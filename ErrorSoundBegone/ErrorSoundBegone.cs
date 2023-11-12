using System.Collections.Generic;
using Dalamud.Game.Command;
using Dalamud.Hooking;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace ErrorSoundBegone
{
    public sealed unsafe class ErrorSoundBegone : IDalamudPlugin
    {
        public string Name => "Error Sound Begone";
        private const string CommandName = "/errorsoundbegone";

        private DalamudPluginInterface PluginInterface { get; init; }
        private ICommandManager CommandManager { get; init; }
        private IGameInteropProvider GameInteropProvider { get; init; }

        private IChatGui ChatGui { get; init; }
        public Configuration Configuration { get; init; }

        public ErrorSoundBegone(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] ICommandManager commandManager,
            [RequiredVersion("1.0")] IGameInteropProvider gameInteropProvider,
            [RequiredVersion("1.0")] IChatGui chatGui)
        {
            this.PluginInterface = pluginInterface;
            this.CommandManager = commandManager;
            this.GameInteropProvider = gameInteropProvider;
            this.ChatGui = chatGui;

            this.Configuration = this.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Configuration.Initialize(this.PluginInterface);

            this.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Toggle muting error sounds. Toggles both without arguments. Use 'click' or 'error' to toggle those specifically"
            });
           
            playSoundEffectHook = GameInteropProvider.HookFromSignature<PlaySoundEffectDelegate>(PlaySoundEffectSignature, PlaySoundEffectDetour);
            playSoundEffectCallerHook = GameInteropProvider.HookFromSignature<PlaySoundEffectCallerDelegate>(PlaySoundEffectCallerSignature,PlaySoundEffectCallerDetour);
            playSoundEffectHook.Enable();
            playSoundEffectCallerHook.Enable();
        }

        public void Dispose()
        {
            this.CommandManager.RemoveHandler(CommandName);
            playSoundEffectHook.Disable();
            playSoundEffectHook.Dispose();
            playSoundEffectCallerHook.Disable();
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

        private delegate void PlaySoundEffectDelegate(uint soundId, void* a2, void* a3, void* a4);

        private delegate byte PlaySoundEffectCallerDelegate(void* a1, byte* data, void* a3, void* a4);

        private readonly Hook<PlaySoundEffectDelegate> playSoundEffectHook;
        private readonly Hook<PlaySoundEffectCallerDelegate> playSoundEffectCallerHook;
        
        private const string PlaySoundEffectSignature = "E8 ?? ?? ?? ?? 4D 39 BE";
        private const string PlaySoundEffectCallerSignature = "40 56 57 41 56 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 41 8B F0";

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
