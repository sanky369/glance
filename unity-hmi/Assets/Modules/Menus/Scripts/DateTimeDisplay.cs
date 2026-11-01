using System;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace Modules.Menus.Scripts
{
    public class DateTimeDisplay : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI _timeText;
        [SerializeField] private TextMeshProUGUI _dateText;

        private int _lastMinute = -1;

        private void Update()
        {
            var now = DateTime.UtcNow.ToLocalTime();
            if (now.Minute == _lastMinute) return;
            _lastMinute = now.Minute;
            _timeText.text = now.ToString("HH:mm", CultureInfo.InvariantCulture);
            _dateText.text = $"{now:ddd} {now.Day}{GetOrdinalSuffix(now.Day)} {now:MMM}";
        }

        private static string GetOrdinalSuffix(int day)
        {
            if (day is >= 11 and <= 13) return "th";
            return (day % 10) switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th",
            };
        }
    }
}
