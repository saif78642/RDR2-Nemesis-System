using System;
using System.Reflection;
using System.Windows.Forms;
using RDR2.Math;

[assembly: AssemblyTitle("ScriptHookRDRNetAPI")]
[assembly: AssemblyVersion("2.2.0.0")]
[assembly: AssemblyFileVersion("2.2.0.0")]

namespace RDR2.Math
{
    public struct Vector3
    {
        public float X;
        public float Y;
        public float Z;
        public Vector3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public static Vector3 Zero => default;
        public float Length() => 0f;
        public void Normalize() { }
        public static Vector3 Normalize(Vector3 v) => default;
        public float DistanceTo(Vector3 position) => 0f;
        public float DistanceTo2D(Vector3 position) => 0f;
        public static float Dot(Vector3 left, Vector3 right) => 0f;
        public static Vector3 Cross(Vector3 left, Vector3 right) => default;
        public static Vector3 operator +(Vector3 left, Vector3 right) => default;
        public static Vector3 operator -(Vector3 left, Vector3 right) => default;
        public static Vector3 operator -(Vector3 value) => default;
        public static Vector3 operator *(Vector3 value, float scale) => default;
        public static Vector3 operator /(Vector3 value, float scale) => default;
        public static bool operator ==(Vector3 left, Vector3 right) => false;
        public static bool operator !=(Vector3 left, Vector3 right) => false;
        public override bool Equals(object obj) => false;
        public override int GetHashCode() => 0;
    }
}

namespace RDR2.UI
{
    public static class Screen
    {
        public static void PrintSubtitle(string text) { }
    }
}

namespace RDR2
{
    public enum BlipType : uint { }
    public enum BoneID : int { }
    public enum eAmmoType : uint { }
    public enum eInputType : uint { }
    public enum eRelationshipType : int { }
    public enum eWeapon : uint { }
    public enum eWeaponAttachPoint : int { }
    public enum PedHash : uint { s_m_m_pinlaw_01 = 737910671u, s_m_m_ambientblwpolice_01 = 2403720683u, s_m_m_ambientsdpolice_01 = 1830978939u, s_m_m_ambientlawrural_01 = 4105441914u, s_m_m_valdeputy_01 = 2521699184u, s_m_m_asbdeputies_01 = 4263638651u, u_m_m_sdpolicechief_01 = 1564488820u }
    public enum VehicleHash : uint { }

    public struct Model
    {
        public Model(int hash) { }
        public Model(uint hash) { }
        public Model(string name) { }
        public Model(PedHash hash) { }
        public int Hash => 0;
        public bool IsHorse => false;
        public bool IsInCdImage => false;
        public bool IsLoaded => false;
        public bool IsPed => false;
        public bool IsValid => false;
        public bool Request(int timeout) => false;
        public static bool operator ==(Model left, Model right) => false;
        public static bool operator !=(Model left, Model right) => false;
        public static implicit operator Model(string source) => default;
        public override bool Equals(object obj) => false;
        public override int GetHashCode() => 0;
    }

    public abstract class PoolObject
    {
        public int Handle { get; protected set; }
        public bool Exists() => false;
        public void Delete() { }
        public static implicit operator int(PoolObject source) => 0;
    }

    public class Blip : PoolObject
    {
        public Blip(int handle) { Handle = handle; }
    }

    public abstract class Entity : PoolObject
    {
        public Entity(int handle) { Handle = handle; }
        public bool IsAnimal => false;
        public bool IsDead => false;
        public int Health { get; set; }
        public int MaxHealth { get; set; }
        public float Heading { get; set; }
        public float HeightAboveGround => 0f;
        public Vector3 ForwardVector => default;
        public Vector3 Position { get; set; }
        public Vector3 RightVector => default;
        public Model Model => default;
        public Blip AddBlip(BlipType blipType) => null;
        public static Entity FromHandle(int handle) => null;
        public static bool operator ==(Entity left, Entity right) => false;
        public static bool operator !=(Entity left, Entity right) => false;
        public override bool Equals(object obj) => false;
        public override int GetHashCode() => 0;
    }

    public class Ped : Entity
    {
        public Ped(int handle) : base(handle) { }
        public bool IsHogtied => false;
        public bool IsHuman => false;
        public bool IsIncapacitated => false;
        public bool IsInCombat => false;
        public bool IsInVehicle => false;
        public bool IsOnMount => false;
        public bool IsPlayer => false;
        public bool IsRagdoll => false;
        public int Accuracy { get; set; }
        public bool BlockPermanentEvents { set { } }
        public uint RelationshipGroup { get; set; }
        public eRelationshipType GetRelationshipWithPed(Ped ped) => default;
        public void GiveWeapon(eWeapon weapon, int ammoCount, eWeaponAttachPoint attachPoint, bool equipNow, bool isAmmoLoaded, bool a6) { }
    }

    public class Vehicle : Entity
    {
        public Vehicle(int handle) : base(handle) { }
    }

    public class Player
    {
        public Ped Character => null;
        public int Handle => 0;
    }

    public class Script
    {
        public Script() { }
        public string BaseDirectory => "";
        public int Interval { get; set; }
        public event EventHandler Aborted;
        public event KeyEventHandler KeyDown;
        public event KeyEventHandler KeyUp;
        public event EventHandler Tick;
    }

    public static class Game
    {
        public static bool IsUsingKeyboardAndMouse => false;
        public static Player Player => null;
        public static int FrameCount => 0;
        public static int GameTime => 0;
        public static bool IsControlJustPressed(eInputType input) => false;
        public static bool IsControlPressed(eInputType input) => false;
        public static bool IsDisabledControlJustPressed(eInputType input) => false;
        public static bool IsDisabledControlPressed(eInputType input) => false;
        public static float GetDisabledControlNormal(eInputType input) => 0f;
        public static void DisableAllControlsThisFrame() { }
        public static void DisableControlThisFrame(eInputType input) { }
    }

    public static class GameplayCamera
    {
        public static Vector3 Direction => default;
    }

    public static class World
    {
        public static Ped CreatePed(PedHash model, Vector3 position, float heading = 0f) => null;
        public static Ped[] GetAllPeds() => null;
        public static Vehicle CreateVehicle(VehicleHash model, Vector3 position, float heading = 0f) => null;
        public static Vehicle[] GetAllVehicles() => null;
        public static uint AddRelationshipGroup(string name) => 0u;
        public static Vector3 GetSafeCoordForPed(Vector3 position, bool onGround = true, int flags = 0) => default;
        public static void SetRelationshipBetweenGroups(eRelationshipType relationship, uint group1, uint group2) { }
    }
}

namespace RDR2.Native
{
    public class InputArgument
    {
        public InputArgument(ulong value) { }
        public static implicit operator InputArgument(bool value) => null;
        public static implicit operator InputArgument(int value) => null;
        public static implicit operator InputArgument(uint value) => null;
        public static implicit operator InputArgument(float value) => null;
        public static implicit operator InputArgument(string value) => null;
    }

    public static class Function
    {
        public static T Call<T>(ulong hash, params InputArgument[] arguments) => default;
        public static void Call(ulong hash, params InputArgument[] arguments) { }
    }
    public static class AUDIO
    {
        public static unsafe bool DOES_CONTEXT_EXIST_FOR_THIS_PED(int p0, string p1, bool p2) => default;
        public static unsafe bool IS_AMBIENT_SPEECH_DISABLED(int p0) => default;
        public static unsafe bool IS_ANY_SPEECH_PLAYING(int p0) => default;
        public static unsafe bool PREPARE_SOUNDSET(string p0, bool p1) => default;
        public static unsafe int GET_SOUND_ID() => default;
        public static unsafe void RELEASE_SOUND_ID(int p0) { }
        public static unsafe void SET_AMBIENT_VOICE_NAME(int p0, string p1) { }
        public static unsafe void STOP_PED_SPEAKING(int p0, bool p1) { }
    }
    public static class CAM
    {
        public static unsafe bool IS_SCREEN_FADED_OUT() => default;
        public static unsafe bool IS_SCREEN_FADING_IN() => default;
        public static unsafe bool IS_SCREEN_FADING_OUT() => default;
        public static unsafe Vector3 GET_FINAL_RENDERED_CAM_COORD() => default;
        public static unsafe Vector3 GET_GAMEPLAY_CAM_COORD() => default;
        public static unsafe Vector3 GET_GAMEPLAY_CAM_ROT(int p0) => default;
    }
    public static class CLOCK
    {
        public static unsafe int GET_CLOCK_DAY_OF_MONTH() => default;
        public static unsafe int GET_CLOCK_HOURS() => default;
        public static unsafe int GET_CLOCK_MINUTES() => default;
        public static unsafe int GET_CLOCK_MONTH() => default;
        public static unsafe int GET_CLOCK_YEAR() => default;
        public static unsafe void PAUSE_CLOCK(bool p0, ulong p1) { }
    }
    public static class DLC
    {
        public static unsafe bool GET_IS_LOADING_SCREEN_ACTIVE() => default;
    }
    public static class ENTITY
    {
        public static unsafe bool _GET_ENTITY_CAN_BE_DAMAGED(int p0) => default;
        public static unsafe bool DOES_ENTITY_EXIST(int p0) => default;
        public static unsafe bool HAS_COLLISION_LOADED_AROUND_ENTITY(int p0) => default;
        public static unsafe bool HAS_ENTITY_BEEN_DAMAGED_BY_ANY_PED(int p0) => default;
        public static unsafe bool HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(int p0, int p1, bool p2, bool p3) => default;
        public static unsafe bool HAS_ENTITY_CLEAR_LOS_TO_ENTITY_IN_FRONT(int p0, int p1, int p2) => default;
        public static unsafe bool HAS_ENTITY_CLEAR_LOS_TO_ENTITY(int p0, int p1, int p2) => default;
        public static unsafe bool IS_ENTITY_A_MISSION_ENTITY(int p0) => default;
        public static unsafe bool IS_ENTITY_A_PED(int p0) => default;
        public static unsafe bool IS_ENTITY_ATTACHED_TO_ENTITY(int p0, int p1) => default;
        public static unsafe bool IS_ENTITY_ATTACHED(int p0) => default;
        public static unsafe bool IS_ENTITY_DEAD(int p0) => default;
        public static unsafe bool IS_ENTITY_PLAYING_ANIM(int p0, string p1, string p2, int p3) => default;
        public static unsafe int GET_ENTITY_BONE_INDEX_BY_NAME(int p0, string p1) => default;
        public static unsafe int GET_ENTITY_HEALTH(int p0) => default;
        public static unsafe float GET_ANIM_DURATION(string p0, string p1) => default;
        public static unsafe float GET_ENTITY_HEADING(int p0) => default;
        public static unsafe float GET_ENTITY_SPEED(int p0) => default;
        public static unsafe uint GET_ENTITY_MODEL(int p0) => default;
        public static unsafe Vector3 GET_ENTITY_COORDS(int p0, bool p1, bool p2) => default;
        public static unsafe Vector3 GET_ENTITY_FORWARD_VECTOR(int p0) => default;
        public static unsafe Vector3 GET_ENTITY_VELOCITY(int p0, int p1) => default;
        public static unsafe Vector3 GET_WORLD_POSITION_OF_ENTITY_BONE(int p0, int p1) => default;
        public static unsafe void ATTACH_ENTITY_TO_ENTITY(int p0, int p1, int p2, float p3, float p4, float p5, float p6, float p7, float p8, bool p9, bool p10, bool p11, bool p12, int p13, bool p14, bool p15, bool p16) { }
        public static unsafe void CLEAR_ENTITY_LAST_DAMAGE_ENTITY(int p0) { }
        public static unsafe void DETACH_ENTITY(int p0, bool p1, bool p2) { }
        public static unsafe void FREEZE_ENTITY_POSITION(int p0, bool p1) { }
        public static unsafe void SET_ENTITY_AS_MISSION_ENTITY(int p0, bool p1, bool p2) { }
        public static unsafe void SET_ENTITY_AS_NO_LONGER_NEEDED(int* p0) { }
        public static unsafe void SET_ENTITY_COLLISION(int p0, bool p1, bool p2) { }
        public static unsafe void SET_ENTITY_COORDS(int p0, float p1, float p2, float p3, bool p4, bool p5, bool p6, bool p7) { }
        public static unsafe void SET_ENTITY_HEADING(int p0, float p1) { }
        public static unsafe void SET_ENTITY_HEALTH(int p0, int p1, int p2) { }
        public static unsafe void SET_ENTITY_INVINCIBLE(int p0, bool p1) { }
        public static unsafe void SET_ENTITY_NO_COLLISION_ENTITY(int p0, int p1, bool p2) { }
        public static unsafe void SET_ENTITY_VELOCITY(int p0, float p1, float p2, float p3) { }
        public static unsafe void SET_ENTITY_VISIBLE(int p0, bool p1) { }
    }
    public static class FIRE
    {
        public static unsafe bool IS_ENTITY_ON_FIRE(int p0) => default;
        public static unsafe bool IS_EXPLOSION_IN_SPHERE(int p0, float p1, float p2, float p3, float p4) => default;
        public static unsafe int GET_NUMBER_OF_FIRES_IN_RANGE(float p0, float p1, float p2, float p3) => default;
        public static unsafe int START_SCRIPT_FIRE(float p0, float p1, float p2, int p3, float p4, bool p5, string p6, float p7, int p8) => default;
        public static unsafe void ADD_EXPLOSION(float p0, float p1, float p2, int p3, float p4, bool p5, bool p6, float p7) { }
        public static unsafe void ADD_OWNED_EXPLOSION(int p0, float p1, float p2, float p3, int p4, float p5, bool p6, bool p7, float p8) { }
    }
    public static class GRAPHICS
    {
        public static unsafe bool GET_SCREEN_COORD_FROM_WORLD_COORD(float p0, float p1, float p2, float* p3, float* p4) => default;
        public static unsafe void DRAW_RECT(float p0, float p1, float p2, float p3, int p4, int p5, int p6, int p7, bool p8, bool p9) { }
        public static unsafe void SET_SCRIPT_GFX_DRAW_ORDER(int p0) { }
    }
    public static class HUD
    {
        public static unsafe bool _UI_PROMPT_HAS_HOLD_MODE_COMPLETED(int p0) => default;
        public static unsafe bool _UI_PROMPT_HAS_STANDARD_MODE_COMPLETED(int p0, int p1) => default;
        public static unsafe bool IS_PAUSE_MENU_ACTIVE() => default;
        public static unsafe int _UI_PROMPT_GET_GROUP_ID_FOR_TARGET_ENTITY(int p0) => default;
        public static unsafe int _UI_PROMPT_REGISTER_BEGIN() => default;
        public static unsafe ulong _UI_PROMPT_SET_CONTROL_ACTION(int p0, uint p1) => default;
        public static unsafe void _HIDE_HUD_THIS_FRAME() { }
        public static unsafe void _UI_PROMPT_DELETE(int p0) { }
        public static unsafe void _UI_PROMPT_DISABLE_PROMPTS_THIS_FRAME() { }
        public static unsafe void _UI_PROMPT_REGISTER_END(int p0) { }
        public static unsafe void _UI_PROMPT_SET_ENABLED(int p0, bool p1) { }
        public static unsafe void _UI_PROMPT_SET_GROUP(int p0, int p1, int p2) { }
        public static unsafe void _UI_PROMPT_SET_STANDARD_MODE(int p0, bool p1) { }
        public static unsafe void _UI_PROMPT_SET_STANDARDIZED_HOLD_MODE(int p0, uint p1) { }
        public static unsafe void _UI_PROMPT_SET_VISIBLE(int p0, bool p1) { }
        public static unsafe void HIDE_HUD_AND_RADAR_THIS_FRAME() { }
    }
    public static class INTERIOR
    {
        public static unsafe bool IS_INTERIOR_READY(int p0) => default;
        public static unsafe int GET_INTERIOR_AT_COORDS(float p0, float p1, float p2) => default;
        public static unsafe int GET_INTERIOR_FROM_ENTITY(int p0) => default;
    }
    public static class LAW
    {
        public static unsafe bool IS_LAW_INCIDENT_ACTIVE(int p0) => default;
        public static unsafe int GET_BOUNTY(int p0) => default;
        public static unsafe uint _GET_HUD_PLAYER_CRIME_TYPE(int p0) => default;
    }
    public static class MAP
    {
        public static unsafe bool DOES_BLIP_EXIST(int p0) => default;
        public static unsafe bool IS_WAYPOINT_ACTIVE() => default;
        public static unsafe int BLIP_ADD_FOR_COORDS(uint p0, float p1, float p2, float p3) => default;
        public static unsafe int GET_BLIP_FROM_ENTITY(int p0) => default;
        public static unsafe void ADD_POINT_TO_GPS_MULTI_ROUTE(float p0, float p1, float p2, bool p3) { }
        public static unsafe void CLEAR_GPS_MULTI_ROUTE() { }
        public static unsafe void REMOVE_BLIP(int* p0) { }
        public static unsafe void SET_BLIP_COORDS(int p0, float p1, float p2, float p3) { }
        public static unsafe void SET_BLIP_SPRITE(int p0, uint p1, bool p2) { }
        public static unsafe void SET_GPS_MULTI_ROUTE_RENDER(bool p0) { }
        public static unsafe void START_GPS_MULTI_ROUTE(uint p0, bool p1, bool p2) { }
    }
    public static class MISC
    {
        public static unsafe bool GET_GROUND_Z_FOR_3D_COORD(float p0, float p1, float p2, float* p3, bool p4) => default;
        public static unsafe bool GET_MISSION_FLAG() => default;
        public static unsafe int UPDATE_ONSCREEN_KEYBOARD() => default;
        public static unsafe string GET_ONSCREEN_KEYBOARD_RESULT() => default;
        public static unsafe uint GET_HASH_KEY(string p0) => default;
        public static unsafe void CANCEL_ONSCREEN_KEYBOARD() { }
        public static unsafe void DISPLAY_ONSCREEN_KEYBOARD(int p0, string p1, string p2, string p3, string p4, string p5, string p6, int p7) { }
        public static unsafe void GET_CURR_WEATHER_STATE(uint* p0, uint* p1, float* p2) { }
        public static unsafe void GET_MODEL_DIMENSIONS(uint p0, Vector3* p1, Vector3* p2) { }
        public static unsafe void SET_TIME_SCALE(float p0) { }
    }
    public static class MONEY
    {
        public static unsafe bool _MONEY_DECREMENT_CASH_BALANCE(int p0) => default;
        public static unsafe bool _MONEY_INCREMENT_CASH_BALANCE(int p0, uint p1) => default;
        public static unsafe int _MONEY_GET_CASH_BALANCE() => default;
    }
    public static class OBJECT
    {
        public static unsafe bool PLACE_OBJECT_ON_GROUND_PROPERLY(int p0, bool p1) => default;
        public static unsafe int CREATE_OBJECT(uint p0, float p1, float p2, float p3, bool p4, bool p5, bool p6, bool p7, bool p8) => default;
        public static unsafe int DOOR_SYSTEM_GET_DOOR_STATE(uint p0) => default;
        public static unsafe void DELETE_OBJECT(int* p0) { }
    }
    public static class PAD
    {
        public static unsafe void DISABLE_ALL_CONTROL_ACTIONS(int p0) { }
        public static unsafe void ENABLE_CONTROL_ACTION(int p0, uint p1, bool p2) { }
    }
    public static class PATHFIND
    {
        public static unsafe bool GET_CLOSEST_VEHICLE_NODE(float p0, float p1, float p2, Vector3* p3, int p4, float p5, float p6) => default;
    }
    public static class PED
    {
        public static unsafe bool _GET_PED_COMBAT_ATTRIBUTE(int p0, int p1) => default;
        public static unsafe bool _IS_PED_CHILD(int p0) => default;
        public static unsafe bool GET_PED_IS_BEING_GRAPPLED(int p0) => default;
        public static unsafe bool GET_PED_LAST_DAMAGE_BONE(int p0, int* p1) => default;
        public static unsafe bool GET_PED_STEALTH_MOVEMENT(int p0) => default;
        public static unsafe bool IS_PED_CARRYING_SOMETHING(int p0) => default;
        public static unsafe bool IS_PED_FATALLY_INJURED(int p0) => default;
        public static unsafe bool IS_PED_FLEEING(int p0) => default;
        public static unsafe bool IS_PED_GROUP_MEMBER(int p0, int p1, bool p2) => default;
        public static unsafe bool IS_PED_HOGTIED(int p0) => default;
        public static unsafe bool IS_PED_HUMAN(int p0) => default;
        public static unsafe bool IS_PED_IN_ANY_TRAIN(int p0) => default;
        public static unsafe bool IS_PED_IN_ANY_VEHICLE(int p0, bool p1) => default;
        public static unsafe bool IS_PED_IN_COMBAT(int p0, int p1) => default;
        public static unsafe bool IS_PED_IN_GROUP(int p0) => default;
        public static unsafe bool IS_PED_IN_MELEE_COMBAT(int p0) => default;
        public static unsafe bool IS_PED_IN_VEHICLE(int p0, int p1, bool p2) => default;
        public static unsafe bool IS_PED_INCAPACITATED(int p0) => default;
        public static unsafe bool IS_PED_LASSOED(int p0) => default;
        public static unsafe bool IS_PED_MALE(int p0) => default;
        public static unsafe bool IS_PED_ON_MOUNT(int p0) => default;
        public static unsafe bool IS_PED_PRONE(int p0) => default;
        public static unsafe bool IS_PED_RAGDOLL(int p0) => default;
        public static unsafe bool IS_PED_READY_TO_RENDER(int p0) => default;
        public static unsafe bool IS_PED_SHOOTING(int p0) => default;
        public static unsafe bool IS_PED_USING_ANY_SCENARIO(int p0) => default;
        public static unsafe bool SET_PED_TO_RAGDOLL(int p0, int p1, int p2, int p3, bool p4, bool p5, string p6) => default;
        public static unsafe int _GET_CARRIER_AS_MOUNT(int p0) => default;
        public static unsafe int _GET_CARRIER_AS_PED(int p0) => default;
        public static unsafe int _GET_LASSO_TARGET(int p0) => default;
        public static unsafe int _GET_LAST_MOUNT(int p0) => default;
        public static unsafe int _GET_PED_GRAPPLER(int p0) => default;
        public static unsafe int _GET_RIDER_OF_MOUNT(int p0, bool p1) => default;
        public static unsafe int GET_MOUNT(int p0) => default;
        public static unsafe int GET_NUM_META_PED_OUTFITS(int p0) => default;
        public static unsafe int GET_PED_ACCURACY(int p0) => default;
        public static unsafe int GET_PED_BONE_INDEX(int p0, int p1) => default;
        public static unsafe int GET_PED_COMBAT_MOVEMENT(int p0) => default;
        public static unsafe int GET_PED_GROUP_INDEX(int p0) => default;
        public static unsafe int GET_PED_NEARBY_PEDS(int p0, ulong* p1, int p2, int p3) => default;
        public static unsafe int GET_PED_SOURCE_OF_DEATH(int p0) => default;
        public static unsafe int GET_RELATIONSHIP_BETWEEN_PEDS(int p0, int p1) => default;
        public static unsafe int GET_VEHICLE_PED_IS_IN(int p0, bool p1) => default;
        public static unsafe uint _GET_PED_INTERACTION_PERSONALITY(int p0) => default;
        public static unsafe uint _GET_PED_META_OUTFIT_HASH(int p0) => default;
        public static unsafe uint GET_PED_CAUSE_OF_DEATH(int p0) => default;
        public static unsafe uint GET_PED_RELATIONSHIP_GROUP_DEFAULT_HASH(int p0) => default;
        public static unsafe uint GET_PED_RELATIONSHIP_GROUP_HASH(int p0) => default;
        public static unsafe Vector3 GET_PED_BONE_COORDS(int p0, int p1, float p2, float p3, float p4) => default;
        public static unsafe void _APPLY_SHOP_ITEM_TO_PED(int p0, uint p1, bool p2, bool p3, bool p4) { }
        public static unsafe void _EQUIP_META_PED_OUTFIT_PRESET(int p0, int p1, bool p2) { }
        public static unsafe void _EQUIP_META_PED_OUTFIT(int p0, uint p1) { }
        public static unsafe void _FAKE_SET_PED_LOCO_INJURED(int p0, bool p1) { }
        public static unsafe void _REFRESH_META_PED_SHOP_ITEMS(int p0, int p1) { }
        public static unsafe void _SET_PED_DESIRED_LOCO_FOR_MODEL(int p0, string p1) { }
        public static unsafe void _SET_PED_INTERACTION_PERSONALITY(int p0, uint p1) { }
        public static unsafe void _UPDATE_PED_VARIATION(int p0, bool p1, bool p2, bool p3, bool p4, bool p5) { }
        public static unsafe void APPLY_DAMAGE_TO_PED(int p0, int p1, int p2, int p3, int p4) { }
        public static unsafe void APPLY_PED_DAMAGE_PACK(int p0, string p1, float p2, float p3) { }
        public static unsafe void DELETE_PED(int* p0) { }
        public static unsafe void DETACH_CARRIABLE_ENTITY(int p0, bool p1, bool p2) { }
        public static unsafe void REGISTER_HATED_TARGETS_AROUND_PED(int p0, float p1) { }
        public static unsafe void REMOVE_PED_DEFENSIVE_AREA(int p0, bool p1) { }
        public static unsafe void REMOVE_PED_FROM_GROUP(int p0) { }
        public static unsafe void RESET_PED_RAGDOLL_TIMER(int p0) { }
        public static unsafe void SET_GROUP_SEPARATION_RANGE(int p0, float p1) { }
        public static unsafe void SET_PED_ACCURACY(int p0, int p1) { }
        public static unsafe void SET_PED_AS_GROUP_MEMBER(int p0, int p1) { }
        public static unsafe void SET_PED_CAN_TELEPORT_TO_GROUP_LEADER(int p0, int p1, bool p2) { }
        public static unsafe void SET_PED_COMBAT_ABILITY(int p0, int p1) { }
        public static unsafe void SET_PED_COMBAT_ATTRIBUTES(int p0, int p1, bool p2) { }
        public static unsafe void SET_PED_COMBAT_MOVEMENT(int p0, int p1) { }
        public static unsafe void SET_PED_COMBAT_RANGE(int p0, int p1) { }
        public static unsafe void SET_PED_CONFIG_FLAG(int p0, int p1, bool p2) { }
        public static unsafe void SET_PED_INJURED_ON_GROUND_BEHAVIOUR(int p0, float p1) { }
        public static unsafe void SET_PED_INTO_VEHICLE(int p0, int p1, int p2) { }
        public static unsafe void SET_PED_KEEP_TASK(int p0, bool p1) { }
        public static unsafe void SET_PED_MAX_MOVE_BLEND_RATIO(int p0, float p1) { }
        public static unsafe void SET_PED_ONTO_MOUNT(int p0, int p1, int p2, bool p3) { }
        public static unsafe void SET_PED_RELATIONSHIP_GROUP_HASH(int p0, uint p1) { }
        public static unsafe void SET_PED_SPHERE_DEFENSIVE_AREA(int p0, float p1, float p2, float p3, float p4, bool p5, bool p6, bool p7) { }
    }
    public static class PLAYER
    {
        public static unsafe bool GET_ENTITY_PLAYER_IS_FREE_AIMING_AT(int p0, int* p1) => default;
        public static unsafe bool GET_PLAYER_INTERACTION_TARGET_ENTITY(int p0, int* p1, bool p2, bool p3) => default;
        public static unsafe bool GET_PLAYER_TARGET_ENTITY(int p0, int* p1) => default;
        public static unsafe bool IS_PLAYER_BEING_ARRESTED(int p0, bool p1) => default;
        public static unsafe bool IS_PLAYER_CONTROL_ON(int p0) => default;
        public static unsafe bool IS_PLAYER_FREE_AIMING_AT_ENTITY(int p0, int p1) => default;
        public static unsafe bool IS_PLAYER_FREE_AIMING(int p0) => default;
        public static unsafe bool IS_PLAYER_TARGETTING_ENTITY(int p0, int p1, bool p2) => default;
        public static unsafe int _GET_ACTIVE_HORSE_FOR_PLAYER(int p0) => default;
        public static unsafe int _GET_SADDLE_HORSE_FOR_PLAYER(int p0) => default;
        public static unsafe int GET_PLAYER_WANTED_LEVEL(int p0) => default;
        public static unsafe void SET_PLAYER_WEAPON_DAMAGE_MODIFIER(int p0, float p1) { }
    }
    public static class SAVE
    {
        public static unsafe bool SAVEGAME_IS_SAVE_PENDING() => default;
    }
    public static class SCRIPTS
    {
        public static unsafe bool GET_EVENT_DATA(int p0, int p1, ulong* p2, int p3) => default;
        public static unsafe bool IS_LOADING_SCREEN_VISIBLE() => default;
        public static unsafe int GET_NUMBER_OF_EVENTS(int p0) => default;
        public static unsafe uint GET_EVENT_AT_INDEX(int p0, int p1) => default;
    }
    public static class SHAPETEST
    {
        public static unsafe int GET_SHAPE_TEST_RESULT(int p0, bool* p1, Vector3* p2, Vector3* p3, int* p4) => default;
        public static unsafe int START_EXPENSIVE_SYNCHRONOUS_SHAPE_TEST_LOS_PROBE(float p0, float p1, float p2, float p3, float p4, float p5, int p6, int p7, int p8) => default;
    }
    public static class STREAMING
    {
        public static unsafe bool DOES_ANIM_DICT_EXIST(string p0) => default;
        public static unsafe bool HAS_ANIM_DICT_LOADED(string p0) => default;
        public static unsafe bool HAS_MODEL_LOADED(uint p0) => default;
        public static unsafe bool IS_IPL_ACTIVE_HASH(uint p0) => default;
        public static unsafe bool IS_MODEL_A_VEHICLE(uint p0) => default;
        public static unsafe bool IS_MODEL_IN_CDIMAGE(uint p0) => default;
        public static unsafe void REMOVE_IPL_HASH(uint p0) { }
        public static unsafe void REQUEST_ANIM_DICT(string p0) { }
        public static unsafe void REQUEST_COLLISION_AT_COORD(float p0, float p1, float p2) { }
        public static unsafe void REQUEST_IPL_HASH(uint p0) { }
        public static unsafe void REQUEST_MODEL(uint p0, bool p1) { }
        public static unsafe void SET_MODEL_AS_NO_LONGER_NEEDED(uint p0) { }
    }
    public static class TASK
    {
        public static unsafe bool DOES_SCENARIO_POINT_EXIST(int p0) => default;
        public static unsafe bool GET_IS_CARRIABLE_ENTITY(int p0) => default;
        public static unsafe bool IS_PED_IN_WRITHE(int p0) => default;
        public static unsafe bool IS_PED_RUNNING(int p0) => default;
        public static unsafe bool IS_PED_SPRINTING(int p0) => default;
        public static unsafe bool IS_PED_WALKING(int p0) => default;
        public static unsafe int _GET_PED_USING_SCENARIO_POINT(int p0) => default;
        public static unsafe int _GET_SCENARIO_POINT_PED_IS_USING(int p0, bool p1) => default;
        public static unsafe int GET_SCENARIO_POINTS_IN_AREA(float p0, float p1, float p2, float p3, ulong* p4, int p5) => default;
        public static unsafe float _GET_SCENARIO_POINT_HEADING(int p0, bool p1) => default;
        public static unsafe Vector3 _GET_SCENARIO_POINT_COORDS(int p0, bool p1) => default;
        public static unsafe void CLEAR_PED_TASKS_IMMEDIATELY(int p0, bool p1, bool p2) { }
        public static unsafe void CLEAR_PED_TASKS(int p0, bool p1, bool p2) { }
        public static unsafe void TASK_AIM_GUN_AT_ENTITY(int p0, int p1, int p2, bool p3, int p4) { }
        public static unsafe void TASK_ARREST_PED(int p0, int p1) { }
        public static unsafe void TASK_CARRIABLE(int p0, uint p1, int p2, int p3, int p4) { }
        public static unsafe void TASK_CLEAR_LOOK_AT(int p0) { }
        public static unsafe void TASK_COMBAT_HATED_TARGETS_AROUND_PED(int p0, float p1, int p2, ulong p3) { }
        public static unsafe void TASK_COMBAT_PED(int p0, int p1, int p2, int p3) { }
        public static unsafe void TASK_COWER(int p0, int p1, int p2, string p3) { }
        public static unsafe void TASK_DISMOUNT_ANIMAL(int p0, int p1, ulong p2, ulong p3, ulong p4, int p5) { }
        public static unsafe void TASK_FOLLOW_NAV_MESH_TO_COORD(int p0, float p1, float p2, float p3, float p4, int p5, float p6, int p7, float p8) { }
        public static unsafe void TASK_FOLLOW_TO_OFFSET_OF_ENTITY(int p0, int p1, float p2, float p3, float p4, float p5, int p6, float p7, bool p8, bool p9, bool p10, bool p11, bool p12, bool p13) { }
        public static unsafe void TASK_GO_STRAIGHT_TO_COORD(int p0, float p1, float p2, float p3, float p4, int p5, float p6, float p7, int p8) { }
        public static unsafe void TASK_GO_TO_COORD_ANY_MEANS(int p0, float p1, float p2, float p3, float p4, int p5, bool p6, int p7, float p8) { }
        public static unsafe void TASK_GO_TO_ENTITY(int p0, int p1, int p2, float p3, float p4, float p5, int p6) { }
        public static unsafe void TASK_HANDS_UP(int p0, int p1, int p2, int p3, bool p4) { }
        public static unsafe void TASK_KNOCKED_OUT(int p0, float p1, bool p2) { }
        public static unsafe void TASK_LEAVE_ANY_VEHICLE(int p0, int p1, int p2) { }
        public static unsafe void TASK_LOOK_AT_ENTITY(int p0, int p1, int p2, int p3, int p4, int p5) { }
        public static unsafe void TASK_MOUNT_ANIMAL(int p0, int p1, int p2, int p3, float p4, int p5, ulong p6, ulong p7) { }
        public static unsafe void TASK_PICKUP_CARRIABLE_ENTITY(int p0, int p1) { }
        public static unsafe void TASK_PLACE_CARRIED_ENTITY_AT_COORD(int p0, int p1, float p2, float p3, float p4, float p5, int p6) { }
        public static unsafe void TASK_PLAY_ANIM(int p0, string p1, string p2, float p3, float p4, int p5, int p6, float p7, bool p8, int p9, bool p10, string p11, bool p12) { }
        public static unsafe void TASK_SMART_FLEE_PED(int p0, int p1, float p2, int p3, int p4, float p5, int p6) { }
        public static unsafe void TASK_STAND_STILL(int p0, int p1) { }
        public static unsafe void TASK_START_SCENARIO_IN_PLACE_HASH(int p0, uint p1, int p2, bool p3, uint p4, float p5, bool p6) { }
        public static unsafe void TASK_TURN_PED_TO_FACE_ENTITY(int p0, int p1, int p2, float p3, float p4, float p5) { }
        public static unsafe void TASK_USE_NEAREST_SCENARIO_TO_COORD_WARP(int p0, float p1, float p2, float p3, float p4, int p5, bool p6, bool p7, bool p8, bool p9) { }
        public static unsafe void TASK_USE_SCENARIO_POINT(int p0, int p1, string p2, int p3, bool p4, bool p5, uint p6, bool p7, float p8, bool p9) { }
        public static unsafe void TASK_VEHICLE_DRIVE_TO_COORD(int p0, int p1, float p2, float p3, float p4, float p5, ulong p6, uint p7, int p8, float p9, float p10) { }
        public static unsafe void TASK_WANDER_STANDARD(int p0, float p1, int p2) { }
    }
    public static class TXD
    {
        public static unsafe bool HAS_STREAMED_TEXTURE_DICT_LOADED(string p0) => default;
        public static unsafe void REQUEST_STREAMED_TEXTURE_DICT(string p0, bool p1) { }
    }
    public static class UIAPPS
    {
        public static unsafe bool IS_ANY_UIAPP_ACTIVE() => default;
        public static unsafe bool IS_ANY_UIAPP_RUNNING() => default;
        public static unsafe bool IS_UIAPP_RUNNING(string p0) => default;
    }
    public static class UIDEBUG
    {
        public static unsafe void _BG_SET_TEXT_COLOR(int p0, int p1, int p2, int p3) { }
        public static unsafe void _BG_SET_TEXT_SCALE(float p0, float p1) { }
    }
    public static class UIFEED
    {
        public static unsafe void _UI_FEED_CLEAR_ALL_CHANNELS() { }
    }
    public static class VEHICLE
    {
        public static unsafe bool IS_VEHICLE_WRECKED(int p0) => default;
        public static unsafe bool SET_VEHICLE_ON_GROUND_PROPERLY(int p0, bool p1) => default;
        public static unsafe int _GET_NUM_DRAFT_VEHICLE_HARNESS_PED(uint p0) => default;
        public static unsafe int _GET_PED_IN_DRAFT_HARNESS(int p0, int p1) => default;
        public static unsafe int GET_PED_IN_VEHICLE_SEAT(int p0, int p1) => default;
        public static unsafe void DELETE_VEHICLE(int* p0) { }
    }
    public static class WATER
    {
        public static unsafe bool GET_WATER_HEIGHT(float p0, float p1, float p2, float* p3) => default;
    }
    public static class WEAPON
    {
        public static unsafe bool GET_CURRENT_PED_WEAPON(int p0, uint* p1, bool p2, int p3, bool p4) => default;
        public static unsafe bool GET_PED_LAST_WEAPON_IMPACT_COORD(int p0, Vector3* p1) => default;
        public static unsafe bool IS_PED_ARMED(int p0, int p1) => default;
        public static unsafe int GET_CURRENT_PED_WEAPON_ENTITY_INDEX(int p0, int p1) => default;
        public static unsafe int MAKE_PED_DROP_WEAPON(int p0, bool p1, int p2, bool p3, bool p4) => default;
        public static unsafe uint _GET_PED_CURRENT_HELD_WEAPON(int p0) => default;
        public static unsafe uint GET_BEST_PED_WEAPON(int p0, bool p1, bool p2) => default;
        public static unsafe uint GET_PED_AMMO_TYPE_FROM_WEAPON(int p0, uint p1) => default;
        public static unsafe uint GET_WEAPONTYPE_GROUP(uint p0) => default;
        public static unsafe void _ADD_AMMO_TO_PED_BY_TYPE(int p0, uint p1, int p2, uint p3) { }
        public static unsafe void REMOVE_ALL_PED_WEAPONS(int p0, bool p1, bool p2) { }
        public static unsafe void SET_CURRENT_PED_WEAPON(int p0, uint p1, bool p2, int p3, bool p4, bool p5) { }
    }
    public static class ZONE
    {
        public static unsafe uint _GET_MAP_ZONE_AT_COORDS(float p0, float p1, float p2, int p3) => default;
    }
}

