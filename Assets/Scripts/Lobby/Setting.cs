using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace Mood.Lobby
{
    [AddComponentMenu("MOOD/Lobby/Setting")]
    [DisallowMultipleComponent]
    public sealed class Setting : MonoBehaviour
    {
        private const string ResolutionPrefKey = "Setting.ResolutionIndex";
        private const string DisplayModePrefKey = "Setting.DisplayModeIndex";
        private const string BgmVolumePrefKey = "Setting.BGMVolume";
        private const string SfxVolumePrefKey = "Setting.SFXVolume";

        [Header("Resolution")]
        [SerializeField] private TMP_Dropdown ResolutionDropdown;

        [Header("Display Mode")]
        [SerializeField] private TMP_Dropdown DisplayModeDropdown;

        [Header("SFX")]
        [SerializeField] private AudioMixer SFX_AudioMixer;
        [SerializeField] private Slider SFX;

        [Header("BGM")]
        [SerializeField] private AudioMixer BGM_AudioMixer;
        [SerializeField] private Slider Music;

        private void Start()
        {
            InitializeDropdown();

            ResolutionDropdown.onValueChanged.AddListener(SetResolution);
            DisplayModeDropdown.onValueChanged.AddListener(SetDisplayMode);
            Music.onValueChanged.AddListener(SetBGMVolume);
            SFX.onValueChanged.AddListener(SetSFXVolume);

            ApplySavedSettings();
        }

        private void InitializeDropdown()
        {
            ResolutionDropdown.ClearOptions();
            DisplayModeDropdown.ClearOptions();

            ResolutionDropdown.AddOptions(new List<string>
            {
                "1280x720 (HD)",
                "1600x900 (HD+)",
                "1920x1080 (FHD)",
                "2560x1440 (QHD)"
            });

            DisplayModeDropdown.AddOptions(new List<string>
            {
                "전체화면",
                "창모드",
                "여백없는 창모드"
            });
        }

        private void ApplySavedSettings()
        {
            int resolutionIndex = Mathf.Clamp(PlayerPrefs.GetInt(ResolutionPrefKey, 2), 0, ResolutionDropdown.options.Count - 1);
            int displayModeIndex = Mathf.Clamp(PlayerPrefs.GetInt(DisplayModePrefKey, 0), 0, DisplayModeDropdown.options.Count - 1);
            float bgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumePrefKey, GetMixerNormalizedVolume(BGM_AudioMixer, "BGMVolume")));
            float sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumePrefKey, GetMixerNormalizedVolume(SFX_AudioMixer, "SFXVolume")));

            ResolutionDropdown.SetValueWithoutNotify(resolutionIndex);
            ResolutionDropdown.RefreshShownValue();

            DisplayModeDropdown.SetValueWithoutNotify(displayModeIndex);
            DisplayModeDropdown.RefreshShownValue();

            Music.SetValueWithoutNotify(bgmVolume);
            SFX.SetValueWithoutNotify(sfxVolume);

            SetDisplayMode(displayModeIndex);
            SetResolution(resolutionIndex);
            SetBGMVolume(bgmVolume);
            SetSFXVolume(sfxVolume);
        }

        private float GetMixerNormalizedVolume(AudioMixer mixer, string parameterName)
        {
            if (mixer != null && mixer.GetFloat(parameterName, out float currentVolume))
            {
                return Mathf.Pow(10f, currentVolume / 20f);
            }

            return 1f;
        }

        private void SetResolution(int index)
        {
            PlayerPrefs.SetInt(ResolutionPrefKey, index);

            switch (index)
            {
                case 0:
                    Screen.SetResolution(1280, 720, Screen.fullScreenMode);
                    Debug.Log("해상도 변경: 1280 x 720");
                    break;

                case 1:
                    Screen.SetResolution(1600, 900, Screen.fullScreenMode);
                    Debug.Log("해상도 변경: 1600 x 900");
                    break;

                case 2:
                    Screen.SetResolution(1920, 1080, Screen.fullScreenMode);
                    Debug.Log("해상도 변경: 1920 x 1080");
                    break;

                case 3:
                    Screen.SetResolution(2560, 1440, Screen.fullScreenMode);
                    Debug.Log("해상도 변경: 2560 x 1440");
                    break;
            }

            PlayerPrefs.Save();
        }

        private void SetDisplayMode(int index)
        {
            PlayerPrefs.SetInt(DisplayModePrefKey, index);

            switch (index)
            {
                case 0:
                    Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                    Debug.Log("전체화면으로 변환");
                    break;

                case 1:
                    Screen.fullScreenMode = FullScreenMode.Windowed;
                    Debug.Log("창모드로 변환");
                    break;

                case 2:
                    Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
                    Debug.Log("여백없는 창모드로 변환");
                    break;
            }

            PlayerPrefs.Save();
        }

        public void SetBGMVolume(float volume)
        {
            float clampedVolume = Mathf.Clamp01(volume);
            float dB = Mathf.Log10(Mathf.Max(0.0001f, clampedVolume)) * 20f;

            BGM_AudioMixer.SetFloat("BGMVolume", dB);
            PlayerPrefs.SetFloat(BgmVolumePrefKey, clampedVolume);
            PlayerPrefs.Save();
        }

        public void SetSFXVolume(float volume)
        {
            float clampedVolume = Mathf.Clamp01(volume);
            float dB = Mathf.Log10(Mathf.Max(0.0001f, clampedVolume)) * 20f;

            SFX_AudioMixer.SetFloat("SFXVolume", dB);
            PlayerPrefs.SetFloat(SfxVolumePrefKey, clampedVolume);
            PlayerPrefs.Save();
        }
    }
}
