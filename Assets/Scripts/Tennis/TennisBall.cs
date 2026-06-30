using UnityEngine;

namespace FieldUnlocked.Tennis
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class TennisBall : MonoBehaviour
    {
        public enum BallState { Idle, InPlay, Out }
        public BallState State { get; private set; } = BallState.Idle;

        [Header("Físicas")]
        public float bounciness   = 0.75f;
        public float flightDrag   = 0.05f;
        public float rollingDrag  = 2f;
        public float spinEffect   = 0.3f;

        [Header("Visuals")]
        public TrailRenderer trail;

        private Rigidbody _rb;
        private Vector3   _spinAxis;
        private float     _spinAmount;
        private int       _bounceCount;

        // Quién golpeó la pelota por última vez (0=jugador, 1=CPU)
        public int LastHitter { get; private set; } = -1;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();

            SphereCollider col = GetComponent<SphereCollider>();
            PhysicsMaterial pm = new PhysicsMaterial("TennisBall");
            pm.bounciness      = bounciness;
            pm.dynamicFriction = 0.5f;
            pm.staticFriction  = 0.5f;
            pm.bounceCombine   = PhysicsMaterialCombine.Maximum;
            col.sharedMaterial = pm;

            SetIdle();
        }

        private void FixedUpdate()
        {
            if (State != BallState.InPlay) return;
            if (_spinAmount > 0.01f)
            {
                Vector3 spinForce = Vector3.Cross(_spinAxis, _rb.linearVelocity.normalized) * _spinAmount;
                _rb.AddForce(spinForce, ForceMode.Acceleration);
            }
        }

        public void Launch(Vector3 velocity, Vector3 spinAxis, float spinAmount, int hitter)
        {
            State        = BallState.InPlay;
            LastHitter   = hitter;
            _spinAxis    = spinAxis;
            _spinAmount  = spinAmount;
            _bounceCount = 0;

            _rb.isKinematic   = false;
            _rb.useGravity    = true;
            _rb.linearDamping          = flightDrag;
            _rb.angularDamping   = 0.1f;
            _rb.linearVelocity       = velocity;
            _rb.angularVelocity = spinAxis * spinAmount * 10f;

            if (trail != null) trail.emitting = true;
        }

        public void SetIdle()
        {
            State               = BallState.Idle;
            _rb.isKinematic     = true;
            _rb.useGravity      = false;
            _rb.linearVelocity         = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            if (trail != null)  trail.emitting = false;
        }

        public void SetOut()
        {
            State       = BallState.Out;
            _rb.linearDamping    = rollingDrag;
            _spinAmount = 0f;
            if (trail != null) trail.emitting = false;
        }

        private void OnCollisionEnter(Collision col)
        {
            if (State != BallState.InPlay) return;
            _spinAmount  *= 0.6f;
            _bounceCount++;
            if (_bounceCount >= 2) SetOut();
        }

        public int   BounceCount   => _bounceCount;
        public void  ResetBounces() => _bounceCount = 0;
    }
}
