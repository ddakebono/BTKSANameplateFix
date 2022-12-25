using ABI_RC.Core.Player;
using UnityEngine;

namespace BTKSANameplateMod
{
    public class NameplateAdjuster : MonoBehaviour
    {
        public bool isFriend;
        public PlayerNameplate playerNameplate;
        
        private bool _isHidden;
        private bool _isMenuOpen;
        private PlayerNameplate _nameplate;
        private CanvasGroup _canvasGroup;
        private const float MaxSize = 1f;

        private void Start()
        {
            if (_nameplate == null)
                _nameplate = GetComponent<PlayerNameplate>();
            if (_canvasGroup == null)
                _canvasGroup = _nameplate.s_Nameplate.GetComponent<CanvasGroup>();
            
            if(_isHidden)
                _nameplate.transform.localScale = Vector3.zero;

            BTKSANameplateMod.ActiveAdjusters.Add(this);
        }

        private void Update()
        {
            if (_isHidden || _isMenuOpen) return;
            if (!BTKSANameplateMod.CloseRangeFade.BoolValue && !BTKSANameplateMod.ScalingEnable.BoolValue) return;
            
            float currentDist = Vector3.Distance(PlayerSetup.Instance._avatar.transform.position, _nameplate.transform.position);

            if (BTKSANameplateMod.CloseRangeFade.BoolValue && (!BTKSANameplateMod.CloseRangeFadeFriends.BoolValue || !isFriend))
            {
                if (currentDist < BTKSANameplateMod.CloseRangeFadeMaxDist.FloatValue && currentDist > BTKSANameplateMod.CloseRangeFadeMinDist.FloatValue)
                {
                    //Find our alpha value
                    float alphaValue = (currentDist - BTKSANameplateMod.CloseRangeFadeMinDist.FloatValue) / (BTKSANameplateMod.CloseRangeFadeMaxDist.FloatValue - BTKSANameplateMod.CloseRangeFadeMinDist.FloatValue);

                    _canvasGroup.alpha = alphaValue;
                }

                if (currentDist >= BTKSANameplateMod.CloseRangeFadeMaxDist.FloatValue)
                    _canvasGroup.alpha = 1;

                if (currentDist <= BTKSANameplateMod.CloseRangeFadeMinDist.FloatValue)
                    _canvasGroup.alpha = 0;
            }

            if (BTKSANameplateMod.ScalingEnable.BoolValue)
            {
                var scaleDiff = MaxSize - BTKSANameplateMod.ScalerMinSize.FloatValue;
                
                if (currentDist <= BTKSANameplateMod.ScalerMaxDist.FloatValue && currentDist >= 0)
                {
                    float nameplateScale = (scaleDiff * (currentDist / BTKSANameplateMod.ScalerMaxDist.FloatValue)) + BTKSANameplateMod.ScalerMinSize.FloatValue;
                    Vector3 newScale = new Vector3(nameplateScale, nameplateScale, nameplateScale);
                    _nameplate.transform.localScale = newScale;
                }
            }
        }

        public void SetHidden(bool state)
        {
            _isHidden = state;
            if (_nameplate == null) return;
            _nameplate.transform.localScale = state ? Vector3.zero : Vector3.one;
        }

        public void MenuToggled(bool state)
        {
            _isMenuOpen = state;
            if (_isHidden || !state || _nameplate == null || _canvasGroup == null) return;
            
            //Reset nameplate to default states
            _nameplate.transform.localScale = Vector3.one;
            _canvasGroup.alpha = 1;
        }

        public void OnConfigUpdated()
        {
            if(!BTKSANameplateMod.ScalingEnable.BoolValue)
                _nameplate.transform.localScale = Vector3.one;
            if (!BTKSANameplateMod.CloseRangeFade.BoolValue)
                _canvasGroup.alpha = 1;
        }

        private void OnDestroy()
        {
            BTKSANameplateMod.ActiveAdjusters.Remove(this);
        }
    }
}