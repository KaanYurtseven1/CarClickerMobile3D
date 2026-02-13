/// <summary>
/// Building types with explicit integer IDs for save-system stability.
/// NEVER change an existing ID — they are baked into PlayerPrefs save keys.
/// You may rename members or add new ones (with new unique IDs) freely.
/// </summary>
public enum BuildingType
{
    StreetDeals              = 0,   // was: AutoClicker
    EscapeDriver             = 1,   // was: NeighborhoodTaxi
    ParkingControlNetwork    = 2,   // was: ParkingMeterNetwork
    CustomGarage             = 3,   // was: CarWashStation
    UndergroundPartsGarage   = 4,   // was: PartsFactory
    StreetRacingCrew         = 5,   // was: SportsGarage
    ExclusiveDealer          = 6,   // was: LuxuryShowRoom
    NightRacingFleet         = 7,   // was: RideSharingFleet
    ShadowLogistics          = 8,   // was: LogisticsCompany
    HighwayInfluenceSystem   = 9,   // was: HighwayTollSystem
    TrafficOverrideSystem    = 10,  // was: SmartTrafficNetwork
    PursuitDisruptionSystem  = 11,  // was: AutonomousTaxiHub
    AdvancedNitroLab         = 12,  // was: HyperloopCargoLine
    PerformanceEngineeringHub = 13, // was: EVGigafactory
    PrototypeVehicleCenter   = 14,  // was: NanoFuelLab
    ExtremeEngineLab         = 15,  // was: MolecularEngineLab
    NeuralDriverInterface    = 16,  // was: Virus_ProofCarOS
    AlternativeFuelNetwork   = 17,  // was: PrototypeWarpEngine
    EliteRaceDistrict        = 18,  // was: SynapticDrivingNetwork
    WWorldBlacklistLeague    = 19,  // was: HydrogenFuelNetwork
    UltimateDriverAI         = 20,  // was: UraniumRaceTrack
    LegendarySpeedCore       = 21,  // was: PlutoniumEnginePlant
    GlobalRacingAuthority    = 22,  // was: Crypto_TollChain
    MythGarage               = 23,  // was: MoonColonyGarage
    EternalSpeedway          = 24,  // was: GalaxyHighway
    LegendCore               = 25,  // was: Galaxy_XRacingLeague
    RacingDominion           = 26,  // was: CarHackAI
    CarGodProtocol           = 27   // was: CarGodCore
}
