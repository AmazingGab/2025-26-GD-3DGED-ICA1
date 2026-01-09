using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace GDEngine.Core.Components
{
    /// <summary>
    /// Simple drive controller:
    /// U/J = forward/back along current facing; H/K = yaw left/right (world-up).
    /// Forward is taken from the world matrix so it matches visual rotation
    /// regardless of row/column axis extraction.
    /// </summary>
    public sealed class SimpleDriveController : Component
    {
        #region Fields
        private float _moveSpeed = 15f;  // units/sec
        private float _turnSpeed = 2.5f;   // radians/sec
        #endregion

        #region Lifecycle Methods
        protected override void Update(float deltaTime)
        {
            if (Transform == null)
                return;

            var k = Keyboard.GetState();

            float yawInput = 0f;
            if (k.IsKeyDown(Keys.A)) yawInput -= 1f;
            if (k.IsKeyDown(Keys.D)) yawInput += 1f;

            float moveInput = 0f;
            if (k.IsKeyDown(Keys.W)) moveInput -= 1f;
            if (k.IsKeyDown(Keys.S)) moveInput += 1f;

            if (moveInput != 0f && yawInput != 0f)
            {
                Vector3 dirForward = Vector3.Normalize(Vector3.TransformNormal(Vector3.Forward, Transform.WorldMatrix));
                Vector3 dirSide = Vector3.Normalize(Vector3.TransformNormal(Vector3.Left, Transform.WorldMatrix));


                dirForward.Y = 0f;
                dirSide.Y = 0f;
                if (dirForward.LengthSquared() > 1e-8f) dirForward.Normalize();
                if (dirSide.LengthSquared() > 1e-8f) dirSide.Normalize();

                var mutualFactor = 1f / (float)System.Math.Sqrt(2f);


                Vector3 worldDelta = -dirForward * (mutualFactor * moveInput * _moveSpeed * deltaTime)
                                     + dirSide * (-mutualFactor * yawInput * _moveSpeed * deltaTime);
                Transform.TranslateBy(worldDelta, worldSpace: true);
                return;
            }

            if (moveInput != 0f)
            {
                Vector3 dir = Vector3.Normalize(Vector3.TransformNormal(Vector3.Forward, Transform.WorldMatrix));
                dir.Y = 0f;
                if (dir.LengthSquared() > 1e-8f) dir.Normalize();
                Vector3 worldDelta = -dir * (moveInput * _moveSpeed * deltaTime);
                Transform.TranslateBy(worldDelta, worldSpace: true);
                return;
            }

            if (yawInput != 0f)
            {
                Vector3 dir = Vector3.Normalize(Vector3.TransformNormal(Vector3.Left, Transform.WorldMatrix));
                dir.Y = 0f;
                if (dir.LengthSquared() > 1e-8f) dir.Normalize();

                Vector3 worldDelta = -dir * (yawInput * _moveSpeed * deltaTime);
                Transform.TranslateBy(worldDelta, worldSpace: true);
            }
        }
        #endregion
    }
}
