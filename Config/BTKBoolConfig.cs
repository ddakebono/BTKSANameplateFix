using System;
using BTKSASelfPortrait.Config;
using MelonLoader;

namespace BTKSANameplateMod.Config
{
    public class BTKBoolConfig : BTKBaseConfig
    {
        public string Category
        {
            get => _internalPref.Category.DisplayName;
        }
        
        public string Name
        {
            get => _internalPref.DisplayName;
        }
        
        public string Description
        {
            get => _internalPref.Description;
        }
        
        public bool BoolValue
        {
            get => _internalPref.Value;
            set => _internalPref.Value = value;
        }

        public Type Type
        {
            get => _internalPref.GetReflectedType();
        }

        public String DialogMessage => _dialogMessage;
        public Action<bool> OnConfigUpdated;

        public bool ConfirmPrompt;

        private readonly MelonPreferences_Entry<bool> _internalPref;
        private string _dialogMessage;

        public BTKBoolConfig(string category, string name, string description, bool defaultValue, string dialogMessage, bool confirmPrompt)
        {
            _dialogMessage = dialogMessage;
            ConfirmPrompt = confirmPrompt;

            _internalPref = MelonPreferences.CreateEntry(category, name, defaultValue, name, description, true);
            _internalPref.OnEntryValueChanged.Subscribe(ConfigUpdated);
            
            BTKSANameplateMod.BTKConfigs.Add(this);
        }

        private void ConfigUpdated(bool old, bool current)
        {
            OnConfigUpdated?.Invoke(current);
        }
    }
}