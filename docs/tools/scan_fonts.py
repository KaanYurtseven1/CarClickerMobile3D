import re, os, sys
from collections import defaultdict

ROOT = r"c:\Users\kaanyurt7\Desktop\Car_Clicker\CarClickerMobile3D"

FONT_MAP = {
    "fe75d96226daefa49b53ffc633e65bd0": "BigStem-Regular.ttf",
    "cdd1332859c262b4fb023b31f9548ae1": "Big Stem Oblique.ttf",
    "2a4e0a7b22cc78349b1449da0667c945": "BigStem-Regular SDF",
    "07fe05e9d49688640a74cdeab2ac57c6": "Big Stem Oblique SDF",
    "fda2d78545fef6c4c8589594ad06b701": "BebasNeue-Regular SDF",
    "8289c01dc9db514499bb618c08dc9cf0": "BebasNeue-Regular.ttf",
    "8f586378b4e144a9851e7b34d9b748ee": "LiberationSans SDF",
    "2e498d1c8094910479dc3e1b768306a4": "LiberationSans SDF-Fallback",
}

BIGSTEM_GUIDS = {
    "fe75d96226daefa49b53ffc633e65bd0",
    "cdd1332859c262b4fb023b31f9548ae1",
    "2a4e0a7b22cc78349b1449da0667c945",
    "07fe05e9d49688640a74cdeab2ac57c6",
}

TMP_UGUI_GUID = "f4688fdb7df04437aeb418b961361dc5"
TMP_3D_GUID = "9541d86e2fd84c1d9990edf0852d74ab"
UI_TEXT_GUID = "5f7201a12d95ffc409449d95f23cf332"

HEADER_RE = re.compile(r"^--- !u!(\d+) &(\d+)")
NAME_RE = re.compile(r"^\s+m_Name:\s*(.*)")
GO_RE = re.compile(r"^\s+m_GameObject:\s*\{fileID:\s*(\d+)\}")
FATHER_RE = re.compile(r"^\s+m_Father:\s*\{fileID:\s*(\d+)\}")
COMP_RE = re.compile(r"^\s+- component:\s*\{fileID:\s*(\d+)\}")
SCRIPT_RE = re.compile(r"m_Script:\s*\{fileID:\s*\d+,\s*guid:\s*([a-f0-9]+)")
FONT_ASSET_RE = re.compile(r"m_fontAsset:\s*\{fileID:\s*\d+,\s*guid:\s*([a-f0-9]+)")
FONT_RE = re.compile(r"m_Font:\s*\{fileID:\s*\d+,\s*guid:\s*([a-f0-9]+)")
TEXT_RE = re.compile(r"^\s+m_text:\s*(.*)")
ACTIVE_RE = re.compile(r"^\s+m_IsActive:\s*(\d+)")
EDITOR_CLASS_RE = re.compile(r"m_EditorClassIdentifier:\s*(.*)")

def scan_file(filepath):
    with open(filepath, "r", encoding="utf-8", errors="replace") as f:
        lines = f.readlines()

    # Pass 1: Build object maps
    names = {}        # GO fileID -> name
    go_active = {}    # GO fileID -> "0" or "1"
    comp_to_go = {}   # component fileID -> GO fileID
    transform_for_go = {}  # GO fileID -> transform fileID
    parent_of_transform = {}  # transform fileID -> parent transform fileID

    cur_type = ""
    cur_id = ""
    cur_go_components = []
    last_go_id = ""

    for line in lines:
        m = HEADER_RE.match(line)
        if m:
            cur_type = m.group(1)
            cur_id = m.group(2)
            continue

        # GameObject (type 1)
        if cur_type == "1":
            mn = NAME_RE.match(line)
            if mn:
                names[cur_id] = mn.group(1).strip()
            ma = ACTIVE_RE.match(line)
            if ma:
                go_active[cur_id] = ma.group(1).strip()
            mc = COMP_RE.match(line)
            if mc:
                comp_to_go[mc.group(1)] = cur_id

        # Transform (type 4) or RectTransform (type 224)
        if cur_type in ("4", "224"):
            mg = GO_RE.match(line)
            if mg:
                go_id = mg.group(1)
                transform_for_go[go_id] = cur_id
                comp_to_go[cur_id] = go_id
            mf = FATHER_RE.match(line)
            if mf:
                parent_of_transform[cur_id] = mf.group(1)

        # Any MonoBehaviour - record GO ref
        if cur_type == "114":
            mg = GO_RE.match(line)
            if mg:
                comp_to_go[cur_id] = mg.group(1)

    # Build hierarchy path
    def get_hierarchy(go_id):
        parts = []
        tid = transform_for_go.get(go_id, "")
        safety = 0
        while tid and tid != "0" and safety < 50:
            gid = comp_to_go.get(tid, "")
            if gid and gid in names:
                parts.insert(0, names[gid])
            ptid = parent_of_transform.get(tid, "")
            tid = ptid
            safety += 1
        return "/".join(parts) if parts else "(root)"

    # Pass 2: Find text components
    results = []
    cur_type = ""
    cur_id = ""
    in_text = False
    comp_type = ""
    text_go = ""
    font_guid = ""
    text_content = ""
    editor_class = ""

    for line in lines:
        m = HEADER_RE.match(line)
        if m:
            # Flush previous
            if in_text and text_go:
                hier = get_hierarchy(text_go)
                fname = FONT_MAP.get(font_guid, f"UNKNOWN({font_guid})" if font_guid else "NO_FONT")
                is_bigstem = font_guid in BIGSTEM_GUIDS
                active = go_active.get(text_go, "?")
                results.append({
                    "hierarchy": hier,
                    "comp_type": comp_type,
                    "font_name": fname,
                    "font_guid": font_guid,
                    "is_bigstem": is_bigstem,
                    "text": text_content[:60],
                    "active": active,
                })

            cur_type = m.group(1)
            cur_id = m.group(2)
            in_text = False
            comp_type = ""
            text_go = ""
            font_guid = ""
            text_content = ""
            editor_class = ""
            continue

        if cur_type == "114":
            # Always track m_GameObject (appears before m_Script in YAML)
            mg = GO_RE.match(line)
            if mg:
                text_go = mg.group(1)

            ms = SCRIPT_RE.search(line)
            if ms:
                sg = ms.group(1)
                if sg == TMP_UGUI_GUID:
                    in_text = True
                    comp_type = "TextMeshProUGUI"
                elif sg == TMP_3D_GUID:
                    in_text = True
                    comp_type = "TextMeshPro3D"
                elif sg == UI_TEXT_GUID:
                    in_text = True
                    comp_type = "UI.Text"

            if in_text:
                mfa = FONT_ASSET_RE.search(line)
                if mfa:
                    font_guid = mfa.group(1)
                mf = FONT_RE.search(line)
                if mf:
                    font_guid = mf.group(1)
                mt = TEXT_RE.match(line)
                if mt:
                    text_content = mt.group(1).strip()
                me = EDITOR_CLASS_RE.search(line)
                if me:
                    editor_class = me.group(1).strip()

    # Flush last
    if in_text and text_go:
        hier = get_hierarchy(text_go)
        fname = FONT_MAP.get(font_guid, f"UNKNOWN({font_guid})" if font_guid else "NO_FONT")
        is_bigstem = font_guid in BIGSTEM_GUIDS
        active = go_active.get(text_go, "?")
        results.append({
            "hierarchy": hier,
            "comp_type": comp_type,
            "font_name": fname,
            "font_guid": font_guid,
            "is_bigstem": is_bigstem,
            "text": text_content[:60],
            "active": active,
        })

    return results


# Collect all files to scan
scenes = []
prefabs = []
for dirpath, dirnames, filenames in os.walk(os.path.join(ROOT, "Assets")):
    # Skip third-party
    rel = os.path.relpath(dirpath, ROOT)
    if "Houidisoft" in rel or "Modular_Track" in rel:
        continue
    for fn in filenames:
        full = os.path.join(dirpath, fn)
        if fn.endswith(".unity"):
            scenes.append(full)
        elif fn.endswith(".prefab"):
            prefabs.append(full)

print("=" * 80)
print(f"SCENES: {len(scenes)} | PREFABS: {len(prefabs)}")
print("=" * 80)

all_correct = []
all_incorrect = []
all_noref = []

for fpath in scenes + prefabs:
    short = os.path.relpath(fpath, ROOT)
    ftype = "SCENE" if fpath.endswith(".unity") else "PREFAB"
    print(f"\nSCAN [{ftype}]: {short}")

    results = scan_file(fpath)
    print(f"  Found {len(results)} text element(s)")

    for r in results:
        entry = f"  [{r['comp_type']}] {short} | {r['hierarchy']} | Font={r['font_name']} | Text='{r['text']}' | Active={r['active']}"
        if not r["font_guid"]:
            all_noref.append(entry)
        elif r["is_bigstem"]:
            all_correct.append(entry)
        else:
            all_incorrect.append(entry + f" | GUID={r['font_guid']}")

print("\n" + "=" * 80)
print(f"TOTAL: {len(all_correct) + len(all_incorrect) + len(all_noref)} text elements")
print("=" * 80)

print(f"\n### CORRECT (BigStem) — {len(all_correct)} ###")
for e in all_correct:
    print(e)

print(f"\n### NON-BIGSTEM (needs change) — {len(all_incorrect)} ###")
for e in all_incorrect:
    print(e)

print(f"\n### NO FONT REFERENCE — {len(all_noref)} ###")
for e in all_noref:
    print(e)
