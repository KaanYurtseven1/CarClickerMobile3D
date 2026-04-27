// Supabase Edge Function: submit-score  (v3 — x-user-token header)
// Receives score components from the client, re-computes the Racer Score server-side,
// performs anti-rollback validation, and updates the leaderboard_scores table.
//
// IMPORTANT: The client sends its ES256 user JWT via the "x-user-token" custom header,
// NOT in the Authorization header. The Authorization header carries the anon key (HS256)
// so the Supabase Edge Functions relay/gateway can verify it. Our function validates the
// user token server-side via GoTrue's auth.getUser(token).
//
// Deploy: supabase functions deploy submit-score --project-ref <your-ref>

import { createClient } from "https://esm.sh/@supabase/supabase-js@2.49.1";
import { serve } from "https://deno.land/std@0.177.0/http/server.ts";

const FUNCTION_VERSION = "v3-x-user-token";

// ─── Score Formula (must match RankingScoreComputer.ComputeFormula in Unity) ───

function computeRacerScore(
  totalMoneyEarned: number,
  totalBuildingCount: number,
  cardLevelSum: number,
  highestBuildingTier: number,
  blacklistTiersCompleted: number,
): number {
  const score =
    Math.pow(totalMoneyEarned, 0.85) * 0.0001 +
    totalBuildingCount * 50 +
    cardLevelSum * 200 +
    highestBuildingTier * 1000 +
    blacklistTiersCompleted * 5000;
  return Math.floor(score);
}

// ─── Validation Limits ───
// These caps prevent obviously hacked values from being accepted.

const LIMITS = {
  maxTotalMoneyEarned: 1e30, // generous cap for late-game
  maxTotalBuildingCount: 28 * 999, // 28 building types × 999 max each
  maxCardLevelSum: 8 * 1000, // 8 cards × generous max level
  maxHighestBuildingTier: 27, // BuildingType.CarGodProtocol = 27
  maxBlacklistTiersCompleted: 6, // 6 tiers total
};

serve(async (req: Request) => {
  // Only accept POST
  if (req.method !== "POST") {
    return new Response(
      JSON.stringify({ ok: false, error: "Method not allowed" }),
      {
        status: 405,
        headers: { "Content-Type": "application/json" },
      },
    );
  }

  try {
    console.log(`[submit-score ${FUNCTION_VERSION}] Request received`);

    // ─── Extract user token from custom header ───
    // The ES256 user JWT is passed in "x-user-token" to avoid the Edge Functions
    // relay rejecting it (the relay only supports HS256 verification).
    // The "Authorization" header carries the anon key for gateway passthrough.
    const userToken = req.headers.get("x-user-token") ?? "";
    if (!userToken) {
      console.error(
        `[submit-score ${FUNCTION_VERSION}] Missing x-user-token header`,
      );
      return new Response(
        JSON.stringify({
          ok: false,
          error: "Missing x-user-token header",
          version: FUNCTION_VERSION,
        }),
        {
          status: 401,
          headers: { "Content-Type": "application/json" },
        },
      );
    }

    // Create service-role client and validate the user's JWT via GoTrue server-side.
    const supabaseUrl = Deno.env.get("SUPABASE_URL")!;
    const supabaseServiceKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
    const serviceClient = createClient(supabaseUrl, supabaseServiceKey);

    console.log(
      `[submit-score ${FUNCTION_VERSION}] Validating user token via GoTrue...`,
    );
    const {
      data: { user },
      error: userError,
    } = await serviceClient.auth.getUser(userToken);
    if (userError || !user) {
      console.error(
        `[submit-score ${FUNCTION_VERSION}] Auth failed:`,
        userError?.message ?? "no user",
      );
      return new Response(
        JSON.stringify({
          ok: false,
          error: "Invalid user token",
          version: FUNCTION_VERSION,
        }),
        {
          status: 401,
          headers: { "Content-Type": "application/json" },
        },
      );
    }
    const userId = user.id;
    console.log(`[submit-score ${FUNCTION_VERSION}] Validated user: ${userId}`);

    // ─── Parse body ───
    const body = await req.json();
    const totalMoneyEarned = Number(body.total_money_earned ?? 0);
    const totalBuildingCount = Number(body.total_building_count ?? 0);
    const cardLevelSum = Number(body.card_level_sum ?? 0);
    const highestBuildingTier = Number(body.highest_building_tier ?? 0);
    const blacklistTiersCompleted = Number(body.blacklist_tiers_completed ?? 0);

    // ─── Validate ranges ───
    if (
      totalMoneyEarned < 0 ||
      totalMoneyEarned > LIMITS.maxTotalMoneyEarned ||
      totalBuildingCount < 0 ||
      totalBuildingCount > LIMITS.maxTotalBuildingCount ||
      cardLevelSum < 0 ||
      cardLevelSum > LIMITS.maxCardLevelSum ||
      highestBuildingTier < 0 ||
      highestBuildingTier > LIMITS.maxHighestBuildingTier ||
      blacklistTiersCompleted < 0 ||
      blacklistTiersCompleted > LIMITS.maxBlacklistTiersCompleted
    ) {
      return new Response(
        JSON.stringify({
          ok: false,
          error: "Score components out of valid range",
        }),
        { status: 400, headers: { "Content-Type": "application/json" } },
      );
    }

    // ─── Compute score server-side ───
    const racerScore = computeRacerScore(
      totalMoneyEarned,
      totalBuildingCount,
      cardLevelSum,
      highestBuildingTier,
      blacklistTiersCompleted,
    );

    // ─── Anti-rollback: fetch current score ───
    // serviceClient was already created above for auth validation

    const { data: existing, error: fetchError } = await serviceClient
      .from("leaderboard_scores")
      .select("racer_score")
      .eq("player_id", userId)
      .single();

    if (fetchError && fetchError.code !== "PGRST116") {
      // PGRST116 = "not found" which is OK for first submission
      console.error("Fetch error:", fetchError);
      return new Response(
        JSON.stringify({ ok: false, error: "Failed to read current score" }),
        { status: 500, headers: { "Content-Type": "application/json" } },
      );
    }

    const currentScore = existing?.racer_score ?? 0;

    // Anti-rollback: only allow score to increase
    if (racerScore < currentScore) {
      return new Response(
        JSON.stringify({
          ok: false,
          error: "Score rollback rejected",
          current_score: currentScore,
          submitted_score: racerScore,
        }),
        { status: 409, headers: { "Content-Type": "application/json" } },
      );
    }

    // ─── Upsert the score ───
    const { error: upsertError } = await serviceClient
      .from("leaderboard_scores")
      .upsert(
        {
          player_id: userId,
          racer_score: racerScore,
          total_money_earned: totalMoneyEarned,
          total_building_count: totalBuildingCount,
          card_level_sum: cardLevelSum,
          highest_building_tier: highestBuildingTier,
          blacklist_tiers_completed: blacklistTiersCompleted,
          updated_at: new Date().toISOString(),
        },
        { onConflict: "player_id" },
      );

    if (upsertError) {
      console.error(
        `[submit-score ${FUNCTION_VERSION}] leaderboard_scores upsert failed`,
        {
          message: upsertError.message,
          code: upsertError.code,
          details: upsertError.details,
          hint: upsertError.hint,
        },
      );
      return new Response(
        JSON.stringify({
          ok: false,
          error: "Failed to save score",
          db_error: {
            message: upsertError.message,
            code: upsertError.code,
            details: upsertError.details,
            hint: upsertError.hint,
          },
        }),
        { status: 500, headers: { "Content-Type": "application/json" } },
      );
    }

    // ─── Fetch new rank ───
    const { data: rankData } = await serviceClient.rpc("get_player_rank", {
      target_player_id: userId,
    });

    const rank = rankData ?? 0;

    console.log(
      `[submit-score ${FUNCTION_VERSION}] Success — user=${userId} score=${racerScore} rank=${rank}`,
    );
    return new Response(
      JSON.stringify({
        ok: true,
        racer_score: racerScore,
        rank,
        version: FUNCTION_VERSION,
      }),
      { status: 200, headers: { "Content-Type": "application/json" } },
    );
  } catch (err) {
    console.error("Unhandled error:", err);
    return new Response(
      JSON.stringify({ ok: false, error: "Internal server error" }),
      { status: 500, headers: { "Content-Type": "application/json" } },
    );
  }
});
