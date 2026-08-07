using System;
using Mood.Events;
using TMPro;
using UnityEngine;

namespace Mood.UI
{
    [AddComponentMenu("MOOD/UI/Ending Result UI")]
    [DisallowMultipleComponent]
    public class EndingResultUI : MonoBehaviour
    {
        [SerializeField] private ScoreboardUI scoreboardUI;
        [SerializeField] private EndingZone endingZone;
        [SerializeField] private TextMeshProUGUI TimerTxt;
        [SerializeField] private TextMeshProUGUI KillCountTxt;
        [SerializeField] private TextMeshProUGUI RankTxt;

        [Header("Rank 기준 시간(초)")]
        [SerializeField] private float rankSSeconds = 120f;
        [SerializeField] private float rankASeconds = 180f;
        [SerializeField] private float rankBSeconds = 300f;

        [Header("Rank 색상")]
        [SerializeField] private Color rankSColor = new Color(1f, 0.84f, 0f);
        [SerializeField] private Color rankAColor = new Color(0.35f, 0.85f, 1f);
        [SerializeField] private Color rankBColor = new Color(0.45f, 1f, 0.45f);
        [SerializeField] private Color rankCColor = new Color(1f, 1f, 1f);

        private bool hasAppliedEndingResult;

        private void Awake()
        {
            if (scoreboardUI == null)
                scoreboardUI = FindFirstObjectByType<ScoreboardUI>();

            if (endingZone == null)
                endingZone = FindFirstObjectByType<EndingZone>();
        }

        private void Update()
        {
            if (hasAppliedEndingResult || endingZone == null || !endingZone.IsCountdownFinished)
                return;

            float clearTime = endingZone.FinishedTime;
            RankInfo rankInfo = GetRankInfo(clearTime);

            if (TimerTxt != null)
                TimerTxt.text = TimeSpan.FromSeconds(clearTime).ToString("mm\\:ss");

            if (KillCountTxt != null && scoreboardUI != null)
                KillCountTxt.text = $"처치 수: {scoreboardUI.killcount}";

            if (RankTxt != null)
            {
                RankTxt.text = rankInfo.Text;
                RankTxt.color = rankInfo.Color;
            }

            hasAppliedEndingResult = true;
        }

        private RankInfo GetRankInfo(float clearTime)
        {
            // 클리어 시간이 짧을수록 더 높은 랭크와 강조 색상을 부여합니다.
            if (clearTime <= rankSSeconds)
                return new RankInfo("S", rankSColor);

            if (clearTime <= rankASeconds)
                return new RankInfo("A", rankAColor);

            if (clearTime <= rankBSeconds)
                return new RankInfo("B", rankBColor);

            return new RankInfo("C", rankCColor);
        }

        private readonly struct RankInfo
        {
            public RankInfo(string text, Color color)
            {
                Text = text;
                Color = color;
            }

            public string Text { get; }
            public Color Color { get; }
        }
    }
}
