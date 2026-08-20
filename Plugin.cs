using System.Collections.Generic;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace PlayerPlusMod
{
    [BepInPlugin("com.aether.playerplus", "Player+ Modu", "1.1.0")]
    [BepInProcess("Among Us.exe")]
    public class Plugin : BasePlugin
    {
        public static PlayerControl CrewmatePlus;
        public static PlayerControl ImpostorPlus;
        public static bool ImpostorPlusUsedKick = false;

        public override void Load()
        {
            var harmony = new Harmony("com.aether.playerplus");
            harmony.PatchAll();
            Log.LogInfo("PLAYER+ MODU BAŞARIYLA YÜKLENDİ!");
        }

        // HER KAREDE ÇALIŞAN BEPINEX UPDATE METODU
        public void Update()
        {
            PlayerControl me = PlayerControl.LocalPlayer;

            if (me != null && PlayerControl.AllPlayerControls != null)
            {
                if (me.Data != null && me.Data.Role != null)
                {
                    // Crewmate Player+ Yetkileri & Kill Timer
                    if (me == CrewmatePlus)
                    {
                        me.Data.Role.CanVent = true;
                        
                        // Kill süresini sabitleme
                        me.SetKillTimer(5.0f);
                    }
                }
            }
        }
    }

    // 1. ROL SEÇİMİ VE SIFIRLAMA
    [HarmonyPatch(typeof(RoleManager), nameof(RoleManager.SelectRoles))]
    public static class SelectRolesPatch
    {
        public static void Postfix()
        {
            Plugin.CrewmatePlus = null;
            Plugin.ImpostorPlus = null;
            Plugin.ImpostorPlusUsedKick = false;

            List<PlayerControl> crewmates = new List<PlayerControl>();
            List<PlayerControl> impostors = new List<PlayerControl>();

            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player.Data.Role.IsImpostor)
                    impostors.Add(player);
                else
                    crewmates.Add(player);
            }

            if (crewmates.Count > 0)
                Plugin.CrewmatePlus = crewmates[Random.Range(0, crewmates.Count)];

            if (impostors.Count > 0)
                Plugin.ImpostorPlus = impostors[Random.Range(0, impostors.Count)];
        }
    }

    // 2. IMPOSTOR'LARIN GÖREV PANELLERİNİ AÇABİLMESİ
    [HarmonyPatch(typeof(Console), nameof(Console.CanUse))]
    public static class ConsoleCanUsePatch
    {
        public static void Postfix(Console __instance, ref float distance, ref bool canUse, ref bool couldUse)
        {
            if (PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer.Data.Role.IsImpostor)
            {
                couldUse = true;
                canUse = distance < __instance.UsableDistance;
            }
        }
    }

    // 3. CREWMATE PLAYER+ KILL ALMA VE IMPOSTOR PLAYER+ DOST ATEŞİ
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
    public static class MurderPatch
    {
        public static bool Prefix(PlayerControl __instance, PlayerControl target)
        {
            if (__instance == Plugin.CrewmatePlus)
            {
                return true;
            }

            if (__instance == Plugin.ImpostorPlus && target.Data.Role.IsImpostor)
            {
                target.Exiled();
                return false;
            }

            return true;
        }
    }

    // 4. IMPOSTOR PLAYER+: TOPLANTISIZ DİREKT OYUNCU ATMA HAKKI
    public static class ImpostorPlusSpecialAbilities
    {
        public static void ExecuteInstaKick(PlayerControl target)
        {
            if (PlayerControl.LocalPlayer != Plugin.ImpostorPlus) return;
            if (Plugin.ImpostorPlusUsedKick) return;

            if (target != null && !target.Data.IsDead)
            {
                target.Exiled();
                Plugin.ImpostorPlusUsedKick = true;
                Debug.Log($"[PLAYER+] {target.name} oyundan direkt atıldı!");
            }
        }
    }
}