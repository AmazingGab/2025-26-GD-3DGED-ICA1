using GDEngine.Core.Entities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace GDEngine.Core.Components.Controllers.Physics
{
    /// <summary>
    /// Physics-based WASD controller: drives a dynamic <see cref="RigidBody"/> by
    /// setting its horizontal linear velocity (XZ) based on keyboard input.
    /// Uses the active camera's yaw (flattened forward/right) as the movement basis.
    /// </summary>
    /// <see cref="RigidBody"/>
    /// <see cref="Camera"/>
    public sealed class PhysicsWASDController : Component
    {
        #region Static Fields
        #endregion

        #region Fields

        private RigidBody _rigidBody = null!;

        private float _moveSpeed = 6f;

        private Keys _forwardKey = Keys.W;
        private Keys _backwardKey = Keys.S;
        private Keys _leftKey = Keys.A;
        private Keys _rightKey = Keys.D;

        private KeyboardState _keyboardState;

        private GameObject _obj;

        #endregion

        #region Properties

        /// <summary>
        /// Base movement speed in world units per second (applied as velocity magnitude).
        /// </summary>
        public float MoveSpeed
        {
            get => _moveSpeed;
            set => _moveSpeed = value > 0f ? value : 0f;
        }

        /// <summary>
        /// Collision object
        /// </summary>
        public GameObject Obj
        {
            get => _obj;
            set => _obj = value;
        }

        /// <summary>
        /// Key used to move forward.
        /// </summary>
        public Keys ForwardKey
        {
            get => _forwardKey;
            set => _forwardKey = value;
        }

        /// <summary>
        /// Key used to move backward.
        /// </summary>
        public Keys BackwardKey
        {
            get => _backwardKey;
            set => _backwardKey = value;
        }

        /// <summary>
        /// Key used to move left (strafe).
        /// </summary>
        public Keys LeftKey
        {
            get => _leftKey;
            set => _leftKey = value;
        }

        /// <summary>
        /// Key used to move right (strafe).
        /// </summary>
        public Keys RightKey
        {
            get => _rightKey;
            set => _rightKey = value;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Computes a flattened (XZ) forward/right basis from the active camera
        /// or, if no active camera is available, from this component's Transform.
        /// </summary>
        private void GetMovementBasis(out Vector3 forward, out Vector3 right)
        {
            forward = Vector3.Forward;
            right = Vector3.Right;

            var scene = GameObject?.Scene;
            var camera = scene?.ActiveCamera;

            if (camera != null)
            {
                // Use camera's orientation
                forward = camera.Transform.Forward;
                right = camera.Transform.Right;
            }
            else if (Transform != null)
            {
                // Fallback: use this object's orientation
                forward = Transform.Forward;
                right = Transform.Right;
            }

            // Flatten to XZ plane
            forward.Y = 0f;
            right.Y = 0f;

            if (forward.LengthSquared() > 0f)
                forward.Normalize();
            else
                forward = Vector3.Forward;

            if (right.LengthSquared() > 0f)
                right.Normalize();
            else
                right = Vector3.Right;
        }

        #endregion

        #region Lifecycle Methods

        protected override void Start()
        {
            if (GameObject == null)
                throw new NullReferenceException(nameof(GameObject));

            _rigidBody = GameObject.GetComponent<RigidBody>()
                         ?? throw new InvalidOperationException(
                             "PhysicsWASDController requires a RigidBody on the same GameObject.");

            if (_rigidBody.BodyType != BodyType.Dynamic)
                System.Diagnostics.Debug.WriteLine(
                    "PhysicsWASDController: RigidBody is not Dynamic; movement may not behave as expected.");
        }


        //attempt to lock rotation
        private void LockRotation()
        {
            if (_rigidBody == null || Transform == null)
                return;

            //getting inverse quaternion to the current transform
            Quaternion fixRot = Quaternion.Inverse(_obj.Transform.Rotation);
            _obj.Transform.RotateToWorld(fixRot);
        }

        protected override void Update(float deltaTime)
        {
            _keyboardState = Keyboard.GetState();

            GetMovementBasis(out var forward, out var right);

            Vector3 moveDir = Vector3.Zero;
            if (_keyboardState.IsKeyDown(_forwardKey)) moveDir += forward;
            if (_keyboardState.IsKeyDown(_backwardKey)) moveDir -= forward;
            if (_keyboardState.IsKeyDown(_rightKey)) moveDir += right;
            if (_keyboardState.IsKeyDown(_leftKey)) moveDir -= right;

            if (moveDir.LengthSquared() > 0f) moveDir.Normalize();

            Vector3 velocity = _rigidBody.LinearVelocity;
            Vector3 targetHorizontal = moveDir * _moveSpeed;

            // Fix to stop it from freezing in place 
            velocity.X = targetHorizontal.X;
            velocity.Z = targetHorizontal.Z;

            _rigidBody.LinearVelocity = velocity;
            _rigidBody.AngularVelocity = Vector3.Zero;
            //lock rotation of object to prevent tipping over
            LockRotation();
        }

        #endregion
    }
}
