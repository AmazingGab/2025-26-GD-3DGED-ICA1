using GDEngine.Core.Audio;
using GDEngine.Core.Events;
using GDEngine.Core.Services;
using GDEngine.Core.Systems;
using GDEngine.Core.Timing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace GDEngine.Core.Components
{
    /// <summary>
    /// Component that emits sound from its GameObject's position. At random intervals, it moves
    /// </summary>

    public sealed class SoundEmitter : Component
    {

        private Random random = new Random();

        private float timeLeft;
        private int min = 10;
        private int max = 40;
        private string sound;


        #region Properties
        public string Sound
        {
            get => sound;
            set => sound = value;
        }

        public int Min
        {
            get => min;
            set => min = value > 0 ? value : 0;
        }

        public int Max
        {
            get => max;
            set => max = value > 0 ? value : 0;
        }
        #endregion


       

        #region Lifecycle Methods

        protected override void Start()
        {
            if (GameObject == null)
                throw new System.NullReferenceException(nameof(GameObject));
            var events = EngineContext.Instance.Events;
            events.Publish(new PlaySfxEvent(sound, 0.5f, true, GameObject.Transform));
        }

        protected override void Update(float deltaTime)
        {
            var events = EngineContext.Instance.Events;

            timeLeft += Time.DeltaTimeSecs;

            if (timeLeft > random.Next(min, max))
            {
                events.Publish(new PlaySfxEvent(sound, 0.5f, true, GameObject.Transform));
                timeLeft = 0;
            }
            
        }

        #endregion
    }
}