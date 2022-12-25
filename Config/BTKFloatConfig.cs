using System;
using BTKSASelfPortrait.Config;
using MelonLoader;

namespace BTKSANameplateMod.Config
{
    public class BTKFloatConfig : BTKBaseConfig
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
        
        public float FloatValue
        {
            get => _internalPref.Value;
            set => _internalPref.Value = value;
        }
        
        public Type Type
        {
            get => _internalPref.GetReflectedType();
        }
        
        public String DialogMessage => _dialogMessage;

        public Action<float> OnConfigUpdated;
        
        public bool ConfirmPrompt;
        public float MinValue;
        public float MaxValue;

        private MelonPreferences_Entry<float> _internalPref;
        private string _dialogMessage;

        public BTKFloatConfig(string category, string name, string description, float defaultValue, float minValue, float maxValue, string dialogMessage, bool confirmPrompt)
        {
            _dialogMessage = dialogMessage;
            ConfirmPrompt = confirmPrompt;
            MinValue = minValue;
            MaxValue = maxValue;
            
            _internalPref = MelonPreferences.CreateEntry(category, name, defaultValue, name, description, true);
            _internalPref.OnEntryValueChanged.Subscribe(ConfigUpdated);

            BTKSANameplateMod.BTKConfigs.Add(this);
        }

        private void ConfigUpdated(float last, float current)
        {
            OnConfigUpdated?.Invoke(current);
        }
    }
}