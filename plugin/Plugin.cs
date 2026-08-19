using System.Collections.Generic;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace PlayerPlusMod
{
    [BepInPlugin("com.seninadin.playerplus", "Player+ Modu", "1.1.0")]
    [BepInProcess("Among Us.exe")]
    public class Plugin : BasePlugin
    {
        public static PlayerControl CrewmatePlus;
        public static PlayerControl ImpostorPlus;

        // Impostor Player+'ın 1 defalık direkt oyuncu atma (Insta-Kick) hakkı
        public static bool ImpostorPlusUsedKick = false;

        public override void Load()
        {
            var harmony = new Harmony("com.seninadin.playerplus");
            harmony.PatchAll();
            Log.LogInfo("PLAYER+ MODU BAŞARIYLA YÜKLENDİ!");
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

            // Crewmate ve Impostor tarafından 1'er kişi seçilir (Toplam 2 Player+)
            if (crewmates.Count > 0)
                Plugin.CrewmatePlus = crewmates[Random.Range(0, crewmates.Count)];

            if (impostors.Count > 0)
                Plugin.ImpostorPlus = impostors[Random.Range(0, impostors.Count)];

            Debug.Log($"[PLAYER+] Crewmate Player+: {Plugin.CrewmatePlus?.name}");
            Debug.Log($"[PLAYER+] Impostor Player+: {Plugin.ImpostorPlus?.name}");
        }
    }

    // 2. İSİM RENGİ, BİRBİRİNİ GÖRME VE DİNAMİKLER
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    public static class PlayerUpdatePatch
    {
        public static void Postfix(PlayerControl __instance)
        {
            PlayerControl me = PlayerControl.LocalPlayer;

            // Eğer yerel oyuncu CrewmatePlus veya ImpostorPlus ise diğer Player+'ı Açık Yeşil görür
            if (me == Plugin.CrewmatePlus || me == Plugin.ImpostorPlus)
            {
                if (__instance == Plugin.CrewmatePlus || __instance == Plugin.ImpostorPlus)
                {
                    // Açık Yeşil Renk (Light Green - #55FF55)
                    __instance.cosmetics.nameText.color = new Color(0.33f, 1.0f, 0.33f, 1.0f);
                }
            }

            // --- IMPOSTORLARIN GÖREV YAPABİLMESİ ---
            if (me.Data.Role.IsImpostor)
            {
                me.Data.Role.CanDoTasks = true;
            }

            // --- CREWMATE PLAYER+ YETKİLERİ ---
            if (me == Plugin.CrewmatePlus)
            {
                // Vent, Sabotaj ve Kill Yetkileri
                me.Data.Role.CanVent = true;
                
                // Kill Cooldown her zaman 5 saniyeye sabitlenir
                if (me.killTimer > 5.0f)
                {
                    me.SetKillTimer(5.0f);
                }
            }
        }
    }

    // 3. IMPOSTOR'LARIN GÖREV PANELLERİNİ AÇABİLMESİ HOOK'U
    [HarmonyPatch(typeof(Console), nameof(Console.CanUse))]
    public static class ConsoleCanUsePatch
    {
        public static void Postfix(Console __instance, ref float distance, ref bool canUse, ref bool couldUse)
        {
            if (PlayerControl.LocalPlayer.Data.Role.IsImpostor)
            {
                couldUse = true;
                canUse = distance < __instance.UsableDistance;
            }
        }
    }

    // 4. CREWMATE PLAYER+'IN KILL ALABİLMESİ & IMPOSTOR PLAYER+'IN DİĞER IMPOSTORLARI KESEBİLMESİ
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
    public static class MurderPatch
    {
        public static bool Prefix(PlayerControl __instance, PlayerControl target)
        {
            // Crewmate Player+ Kill Alabilir
            if (__instance == Plugin.CrewmatePlus)
            {
                return true;
            }

            // Impostor Player+ Diğer Impostor'ları da Öldürebilir
            if (__instance == Plugin.ImpostorPlus && target.Data.Role.IsImpostor)
            {
                target.Exiled(); // Hedefi direkt eler
                return false;
            }

            return true;
        }
    }

    // 5. IMPOSTOR PLAYER+: TOPLANTISIZ DİREKT OYUNCU ATMA HAKKI (Tek Kullanımlık)
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
                Debug.Log($"[PLAYER+] {target.name} oyuncusu Impostor+ tarafından direkt atıldı!");
            }
        }
    }
}