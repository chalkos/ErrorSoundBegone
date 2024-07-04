using System.Collections.Generic;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Hooking;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;

namespace ErrorSoundBegone
{
    public sealed unsafe class ErrorSoundBegone : IDalamudPlugin
    {
        // services
        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
        [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
        
        // hooks
        private delegate void PlaySoundEffectDelegate(uint soundId, void* a2, void* a3, void* a4);
        [Signature("E8 ?? ?? ?? ?? 4D 39 A6", DetourName = nameof(PlaySoundEffectDetour))]
        private Hook<PlaySoundEffectDelegate> playSoundEffectHook;
        
        private delegate byte PlaySoundEffectCallerDelegate(void* a1, byte* data, void* a3, void* a4);
        [Signature("48 89 5C 24 ?? 55 56 57 48 8D 6C 24 ?? 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 37 41 8B F8 48 8B DA 48 8B F1 45 84 C9", DetourName = nameof(PlaySoundEffectCallerDetour))]
        private Hook<PlaySoundEffectCallerDelegate> playSoundEffectCallerHook;

        // plugin
        public string Name => "Error Sound Begone";
        private const string CommandName = "/errorsoundbegone";

        public Configuration Configuration { get; init; }

        public ErrorSoundBegone()
        {
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Toggle muting error sounds. Toggles both without arguments. Use 'click' or 'error' to toggle those specifically"
            });
            
            GameInteropProvider.InitializeFromAttributes(this);
            playSoundEffectHook.Enable();
            playSoundEffectCallerHook.Enable();
        }

        public void Dispose()
        {
            CommandManager.RemoveHandler(CommandName);
            playSoundEffectHook.Dispose();
            playSoundEffectCallerHook.Dispose();
        }

        private void OnCommand(string command, string args)
        {
            switch (args)
            {
                case "error":
                    Configuration.FilterErrorSounds = !Configuration.FilterErrorSounds;
                    Configuration.Save();
                    break;
                case "click":
                    Configuration.FilterClickSounds = !Configuration.FilterClickSounds;
                    Configuration.Save();
                    break;
                default:
                    Configuration.FilterErrorSounds = Configuration.FilterClickSounds =
                        !(Configuration.FilterErrorSounds || Configuration.FilterClickSounds);
                    Configuration.Save();
                    break;
            }
            ChatGui.Print("Error sounds: " + (Configuration.FilterErrorSounds ? "muted":"enabled"));
            ChatGui.Print("Click sounds: " + (Configuration.FilterClickSounds ? "muted":"enabled"));
        }

        // detour
        
        private bool suppressErrorSound = true;

        private List<byte> errorCodes = new()
        {
            0x54, //target not in range
            0x43, //cannot use while casting
            0x49, //invalid target
            0x43, //cannot use yet
        };

        private void PlaySoundEffectDetour(uint soundId, void* a2, void* a3, void* a4)
        {
            if (Configuration.FilterErrorSounds && soundId == 7u && suppressErrorSound) return;
            if (Configuration.FilterClickSounds && soundId == 12u) return;
            
            playSoundEffectHook.Original.Invoke(soundId, a2, a3, a4);
        }

        private byte PlaySoundEffectCallerDetour(void* a1, byte* data, void* a3, void* a4)
        {
            if (data != null && errorCodes.Contains(*data))
            {
                suppressErrorSound = true;
            }

            /*else if(data != null)
            {
                PluginLog.Warning($"SoundData: {*data:X}");
            }*/
            byte res = playSoundEffectCallerHook.Original.Invoke(a1, data, a3, a4);
            suppressErrorSound = false;
            return res;
        }
    }
}
