using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDEngine.Core.Components
{

    /// <summary>
    /// Represents the state of the player in the game.
    /// </summary>
    public sealed class PlayerState : Component
    {
        private int _insight = 0;
        private bool _isExamining;
        private bool _hasKey;
        private bool _hasHammer;
        private int _unlockedProgress;
        private string _currentMusic = "confused music";

        public int Insight
        {
            get => _insight;
            private set => _insight = value;
        }

        public bool IsExamining
        {
            get => _isExamining;
            private set => _isExamining = value;
        }

        public bool HasKey
        {
            get => _hasKey;
            private set => _hasKey = value;
        }

        public bool HasHammer
        {
            get => _hasHammer;
            private set => _hasHammer = value;
        }

        public int UnlockedProgress
        {
            get => _unlockedProgress;
            private set => _unlockedProgress = value;
        }
        public string CurrentMusic
        {
            get => _currentMusic;
            private set => _currentMusic = value;
        }


        public void AddInsight(int amount)
        {
            _insight += amount;
        }

        public void SetExamining(bool isExamining)
        {
            _isExamining = isExamining;
        }

        public void ObtainKey()
        {
            _hasKey = true;
        }

        public void ObtainHammer()
        {
            _hasHammer = true;
        }

        public void IncreaseProgress()
        {
            _unlockedProgress++;
        }

        public void SetCurrentMusic(string music)
        {
            _currentMusic = music;
        }

    }
}
