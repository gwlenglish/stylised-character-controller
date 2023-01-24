using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using System;
public enum WalkingOnWall : byte
{
    North,
    South,
    East,
    West
}

public static class Helpers
{
    public static Vector3 UpRightDirection(WalkingOnWall wall)
    {
        switch (wall)
        {
            case WalkingOnWall.North:
                return new Vector3(0, -1, 0);
            case WalkingOnWall.South:
                return new Vector3(0, 1, 0);//world coordins
            case WalkingOnWall.East:
                //newDirection = new Vector3(y, z, x);
                return new Vector3(-1, 0, 0);
            case WalkingOnWall.West:
                return new Vector3(1, 0, 0);
            default:
                return new Vector3(0, 0, 0);
        }
    }
}

public class ClientCharacter : Character
{
    public Action OnStartWalking;
    public Action OnStopWalking;
    public Action OnLanded;
    bool walking = false;

    public ClientCharacter(PhysicsBasedCharacterController cc) : base(cc)
    {

    }

    public override void UpdateTick()
    {
        base.UpdateTick();

        switch (cc.State)
        {
            case CharacterState.Grounded:
                if (cc.IsLocalPlayer)
                {
                    if (cc.MoveContext.magnitude > 0)
                    {
                        OnStartWalking?.Invoke();
                        if (!walking)
                        {
                            walking = true;

                        }
                    }
                    else
                    {
                        OnStopWalking?.Invoke();


                    }



                }
                break;
        }
       
    }
}

public class CharacterLand : Character
{
    public CharacterLand(PhysicsBasedCharacterController cc) : base(cc)
    {

    }
}
public class CharacterJumpUp : Character
{
    float timer = 0;
    float jumpwait = .5f;
    public CharacterJumpUp(PhysicsBasedCharacterController cc) : base(cc)
    {

    }

    public override void OnEnter()
    {
        base.OnEnter();
        cc.Rb.AddForce(Helpers.UpRightDirection(cc.Wall) * cc.JumpFactor, ForceMode.Impulse); // This does not work very consistently... Jump height is affected by initial y velocity and y position relative to RideHeight... Want to adopt a fancier approach (more like PlayerMovement). A cheat fix to ensure consistency has been issued above...
        timer = 0;
    }

    public override void FixedTick()
    {
        base.FixedTick();
        var grounded = cc.Grounded;

        cc.CheckGrounded();

        var _gravitationalForce = -Helpers.UpRightDirection(cc.Wall) * cc.Gravity * cc.Rb.mass;

        float fallingvel = 0;
        switch (cc.Wall)
        {
            case WalkingOnWall.South:
                fallingvel = cc.Rb.velocity.y;
                break;
            case WalkingOnWall.North:
                fallingvel = -cc.Rb.velocity.y;
                break;
            case WalkingOnWall.West:
                fallingvel = cc.Rb.velocity.x;
                break;
            case WalkingOnWall.East:
                fallingvel = -cc.Rb.velocity.x;
                break;

        }

        //cc.Rb.AddForce(_gravitationalForce * (cc.FallGravityFactor - 1f)); // Hmm... this feels a bit weird. I want a reactive jump, but I don't want it to dive all the time...

        //  Debug.Log($"Falling: {fallingvel} and Gronded{grounded}");
        if (fallingvel <= 0)//this is is
        {
            cc.Rb.AddForce(_gravitationalForce * (cc.FallGravityFactor - 1f)); // Hmm... this feels a bit weird. I want a reactive jump, but I don't want it to dive all the time...
        }
        else if (fallingvel > 0)
        {
            if (cc._isJumping && cc.JumpInput != Vector3.zero)
            {
                cc.Rb.AddForce(_gravitationalForce * (cc.RiseGravityFactor - 1f));
            }
            else if 
            (cc.JumpInput == Vector3.zero)
            {
                // Impede the jump height to achieve a low jump.
                cc.Rb.AddForce(_gravitationalForce * (cc.LowJumpFactor - 1f));
            }
           
        }

        timer += Time.deltaTime;
        if (timer >= jumpwait)
        {
            if (cc.Grounded)
            {
                cc.ChangeState(CharacterState.Grounded);
            }
        }
       
    }


}
public class CharacterStateControlled : Character
{
    public event Action OnLanded;
    public event Action OnWalking;
    public event Action OnStopWalking;
    public event Action OnJump;
    public event Action<bool> OnJumpReady;
    Vector3 _gravitationalForce;
    Vector3 _moveInput;
    Vector3 _moveForceScale;
    Vector3 _m_GoalVel;

    private bool didLastRayHit;
    private Quaternion _uprightTargetRot = Quaternion.identity;
    private Quaternion _lastTargetRot = Quaternion.identity;
    private Vector3 _platformInitRot = Vector3.zero;
    private bool _shouldMaintainHeight;

    private Vector3 _previousVelocity;


    public CharacterStateControlled(PhysicsBasedCharacterController cc) : base(cc)
    {

    }

    public override void OnEnter()
    {
        base.OnEnter();
        _shouldMaintainHeight = true;
        cc._jumpReady = true;
        OnJumpReady?.Invoke(true);
    }
    private void CharacterJump(Vector3 jumpInput, bool grounded, RaycastHit rayHit)
    {
        cc._timeSinceJumpPressed += Time.fixedDeltaTime;
        cc._timeSinceJump += Time.fixedDeltaTime;
        float fallingvel = 0;
        switch (cc.Wall)
        {
            case WalkingOnWall.South:
                fallingvel = cc.Rb.velocity.y;
                break;
            case WalkingOnWall.North:
                fallingvel = -cc.Rb.velocity.y;
                break;
            case WalkingOnWall.West:
                fallingvel = cc.Rb.velocity.x;
                break;
            case WalkingOnWall.East:
                fallingvel = -cc.Rb.velocity.x;
                break;

        }

      //  Debug.Log($"Falling: {fallingvel} and Gronded{grounded}");
        //if (fallingvel <= 0)//this is is
        //{
        //    _shouldMaintainHeight = true;
        //    cc._jumpReady = true;
        //    OnJumpReady?.Invoke(true);
        //    if (!grounded)
        //    {
        //        // Increase downforce for a sudden plummet.
        //        cc.Rb.AddForce(_gravitationalForce * (cc.FallGravityFactor - 1f)); // Hmm... this feels a bit weird. I want a reactive jump, but I don't want it to dive all the time...
        //    }
        //}
        //else if (fallingvel > 0)
        //{
        //    if (!grounded)
        //    {
        //        if (cc._isJumping)
        //        {
        //            cc.Rb.AddForce(_gravitationalForce * (cc.RiseGravityFactor - 1f));
        //        }
        //        if (jumpInput == Vector3.zero)
        //        {
        //            // Impede the jump height to achieve a low jump.
        //            cc.Rb.AddForce(_gravitationalForce * (cc.LowJumpFactor - 1f));
        //        }
        //    }
        //}

        if (cc._timeSinceJumpPressed < cc.JumpBuffer)
        {
            if (cc._timeSinceUngrounded < cc.CoyoteTime)
            {
                if (cc._jumpReady)
                {
                    OnJumpReady?.Invoke(false);
                    cc._jumpReady = false;
                    _shouldMaintainHeight = false;
                    cc._isJumping = true;
                    cc.Rb.velocity = Vector3.Scale(cc.Rb.velocity, _moveForceScale);
                    // _rb.velocity = new Vector3(_rb.velocity.x, 0f, _rb.velocity.z); // Cheat fix... (see comment below when adding force to rigidbody).
                    if (rayHit.distance != 0) // i.e. if the ray has hit
                    {
                        //   _rb.position = new Vector3(_rb.position.x, _rb.position.y - (rayHit.distance - _rideHeight), _rb.position.z);
                    }
                    
                    
                    //cc.Rb.AddForce(Helpers.UpRightDirection(cc.Wall) * cc.JumpFactor, ForceMode.Impulse); // This does not work very consistently... Jump height is affected by initial y velocity and y position relative to RideHeight... Want to adopt a fancier approach (more like PlayerMovement). A cheat fix to ensure consistency has been issued above...
                    
                    
                    cc._timeSinceJumpPressed = cc.JumpBuffer; // So as to not activate further jumps, in the case that the player lands before the jump timer surpasses the buffer.
                    cc._timeSinceJump = 0f;

                    OnJump?.Invoke();
                    cc.ChangeState(CharacterState.Airborne);
                    // FindObjectOfType<AudioManager>().Play("Jump");
                }
            }
        }
    }

    



    private void CalculateTargetRotation(Vector3 yLookAt, RaycastHit rayHit = new RaycastHit())
    {
        if (didLastRayHit)
        {
            _lastTargetRot = _uprightTargetRot;
            if (cc.Platform != null)
            {
                _platformInitRot = cc.Platform.transform.rotation.eulerAngles;
            }
            else
            {
                _platformInitRot = Vector3.zero;
            }

        }
        if (rayHit.rigidbody == null)
        {
            didLastRayHit = true;
        }
        else
        {
            didLastRayHit = false;
        }

        if (yLookAt != Vector3.zero)
        {
            _uprightTargetRot = Quaternion.LookRotation(yLookAt, Helpers.UpRightDirection(cc.Wall));
            _lastTargetRot = _uprightTargetRot;
            if (cc.Platform != null)
            {
                _platformInitRot = cc.Platform.transform.rotation.eulerAngles;
            }
            else
            {
                _platformInitRot = Vector3.zero;
            }

        }
        else
        {
            if (cc.Platform != null)
            {
                Vector3 platformRot = cc.Platform.transform.rotation.eulerAngles;//get the rotation here that's not always y
                Vector3 deltaPlatformRot = platformRot - _platformInitRot;
                float yAngle = _lastTargetRot.eulerAngles.y + deltaPlatformRot.y;
                _uprightTargetRot = Quaternion.Euler(new Vector3(0f, yAngle, 0f));
                switch (cc.Platform.Wall)
                {
                    case WalkingOnWall.North:
                        _uprightTargetRot = Quaternion.LookRotation(cc.Platform.transform.forward, Helpers.UpRightDirection(cc.Platform.Wall));
                        break;
                    case WalkingOnWall.South:

                        _uprightTargetRot = Quaternion.LookRotation(cc.Platform.transform.forward, Helpers.UpRightDirection(cc.Platform.Wall));
                        break;
                    case WalkingOnWall.East:

                        _uprightTargetRot = Quaternion.LookRotation(cc.Platform.transform.right, Helpers.UpRightDirection(cc.Platform.Wall));
                        break;
                    case WalkingOnWall.West:

                        _uprightTargetRot = Quaternion.LookRotation(cc.Platform.transform.right, Helpers.UpRightDirection(cc.Platform.Wall));
                        break;
                    default:
                        _uprightTargetRot = Quaternion.Euler(new Vector3(0f, yAngle, 0f));
                        break;
                }

                //   _uprightTargetRot = Quaternion.AngleAxis(yAngle, Helpers.UpRightDirection(platform.Wall));
                //    _uprightTargetRot = Quaternion.LookRotation(platform.transform.forward, Helpers.UpRightDirection(platform.Wall));
            }

        }
    }
    private void MaintainUpright(Vector3 yLookAt, RaycastHit rayHit = new RaycastHit())
    {
        CalculateTargetRotation(yLookAt, rayHit);

        Quaternion currentRot = cc.transform.rotation;
        Quaternion toGoal = MathsUtils.ShortestRotation(_uprightTargetRot, currentRot);

        Vector3 rotAxis;
        float rotDegrees;

        toGoal.ToAngleAxis(out rotDegrees, out rotAxis);
        rotAxis.Normalize();

        float rotRadians = rotDegrees * Mathf.Deg2Rad;

        cc.Rb.AddTorque((rotAxis * (rotRadians * cc.UpRightSpringStrength)) - (cc.Rb.angularVelocity * cc.UpRightSpringDamper));
    }


    private void MaintainHeight(RaycastHit rayHit)
    {
        Vector3 vel = cc.Rb.velocity;
        Vector3 otherVel = Vector3.zero;
        Rigidbody hitBody = rayHit.rigidbody;
        if (hitBody != null)
        {
            otherVel = hitBody.velocity;
        }
        var currentdown = -Helpers.UpRightDirection(cc.Wall);
        float rayDirVel = Vector3.Dot(currentdown, vel);
        float otherDirVel = Vector3.Dot(currentdown, otherVel);

        float relVel = rayDirVel - otherDirVel;
        float currHeight = rayHit.distance - cc.RideHeight;
        float springForce = (currHeight * cc.RideSpringStrength) - (relVel * cc.RideSpringDamper);
        Vector3 maintainHeightForce = -_gravitationalForce + springForce * currentdown;
        Vector3 oscillationForce = springForce * currentdown;
        cc.Rb.AddForce(maintainHeightForce);
        cc.SquashAndStretch.ApplyForce(oscillationForce);
        //Debug.DrawLine(transform.position, transform.position + (_rayDir * springForce), Color.yellow);

        // Apply force to objects beneath
        if (hitBody != null)
        {
            hitBody.AddForceAtPosition(-maintainHeightForce, rayHit.point);
        }
    }
    private void CharacterMove(Vector3 moveInput, RaycastHit rayHit)
    {
        Vector3 m_UnitGoal = moveInput;
        Vector3 unitVel = _m_GoalVel.normalized;
        float velDot = Vector3.Dot(m_UnitGoal, unitVel);
        float accel = cc.Acceleration * cc.AccelerationFactorFromDot.Evaluate(velDot);
        Vector3 goalVel = m_UnitGoal * cc.MaxSpeed * cc.SpeedFactor;
        Vector3 otherVel = Vector3.zero;
        Rigidbody hitBody = rayHit.rigidbody;
        _m_GoalVel = Vector3.MoveTowards(_m_GoalVel,
                                        goalVel,
                                        accel * Time.fixedDeltaTime);
        Vector3 neededAccel = (_m_GoalVel - cc.Rb.velocity) / Time.fixedDeltaTime;
        float maxAccel = cc.MaxAccelForce * cc.MaxAccelerationForceFactorFromDot.Evaluate(velDot) * cc.MaxAccelForceFactor;
        neededAccel = Vector3.ClampMagnitude(neededAccel, maxAccel);




        cc.Rb.AddForceAtPosition(Vector3.Scale(neededAccel * cc.Rb.mass, (_moveForceScale)), cc.transform.position + new Vector3(0, cc.transform.localScale.y * cc.LeanFactor, 0));// new Vector3(0f, transform.localScale.y * _leanFactor, 0f)); // Using AddForceAtPosition in order to both move the player and cause the play to lean in the direction of input.
    }
 
    private Vector3 GetLookDirection(lookDirectionOptions lookDirectionOption)
    {
        Vector3 lookDirection = Vector3.zero;
        if (lookDirectionOption == lookDirectionOptions.velocity || lookDirectionOption == lookDirectionOptions.acceleration)
        {
            Vector3 velocity = cc.Rb.velocity;
            velocity.y = 0f;
            if (lookDirectionOption == lookDirectionOptions.velocity)
            {
                lookDirection = velocity;
            }
            else if (lookDirectionOption == lookDirectionOptions.acceleration)
            {
                Vector3 deltaVelocity = velocity - _previousVelocity;
                _previousVelocity = velocity;
                Vector3 acceleration = deltaVelocity / Time.fixedDeltaTime;
                lookDirection = acceleration;
            }
        }
        else if (lookDirectionOption == lookDirectionOptions.moveInput)
        {
            lookDirection = _moveInput;
        }



        return lookDirection;
    }

    private Vector3 AdjustInputToFaceCamera(Vector3 moveInput)
    {
        float facing = Camera.main.transform.eulerAngles.y;
        return (Quaternion.Euler(0, facing, 0) * moveInput);
    }

    public override void FixedTick()
    {
        base.FixedTick();

        if (NetworkManager.Singleton.IsServer == false) return;

        _moveInput = new Vector3(cc.MoveContext.x, 0, cc.MoveContext.y);
        _gravitationalForce = -Helpers.UpRightDirection(cc.Wall) * cc.Gravity * cc.Rb.mass;
        switch (cc.Wall)
        {
            case WalkingOnWall.North:
                _moveInput = new Vector3(cc.MoveContext.x, 0, cc.MoveContext.y);
                _moveForceScale = new Vector3(1, 0, 1);
                break;
            case WalkingOnWall.South:
                _moveInput = new Vector3(cc.MoveContext.x, 0, cc.MoveContext.y);
                _moveForceScale = new Vector3(1, 0, 1);
                break;
            case WalkingOnWall.East:
                _moveInput = new Vector3(0, cc.MoveContext.y, -cc.MoveContext.x);
                _moveForceScale = new Vector3(0, 1, 1);
                break;
            case WalkingOnWall.West:
                _moveInput = new Vector3(0, cc.MoveContext.y, cc.MoveContext.x);
                _moveForceScale = new Vector3(0, 1, 1);
                break;
        }

        if (cc.FaceCamera)
        {
            _moveInput = AdjustInputToFaceCamera(_moveInput);
        }

      
        cc.Grounded = cc.CheckGrounded();

        if (cc.Grounded == true)
        {
            if (cc._prevGrounded == false)
            {
                OnLanded?.Invoke();
            }

            if (_moveInput.magnitude != 0)
            {
                OnWalking?.Invoke();
            }
            else
            {
                OnStopWalking?.Invoke();
            }

            cc._timeSinceUngrounded = 0f;


        }
        else
        {
            OnStopWalking?.Invoke();
            cc._timeSinceUngrounded += Time.fixedDeltaTime;
        }

        CharacterMove(_moveInput, cc.RayHit);
        CharacterJump(cc.JumpInput, cc.Grounded, cc.RayHit);
        if (cc.RayHitGround && _shouldMaintainHeight)
        {
            MaintainHeight(cc.RayHit);
        }

        Vector3 lookDirection = GetLookDirection(cc.LookDirection);
        if (cc.RayHitGround)
        {
            MaintainUpright(lookDirection, cc.RayHit);
        }
       

        cc._prevGrounded = cc.Grounded;
    }
}
public class Character : EntityStates
{
    protected PhysicsBasedCharacterController cc;
    public Character(PhysicsBasedCharacterController cc)
    {
        this.cc = cc;
    }
}
public class EntityStates : IState
{
    public virtual void FixedTick()
    {

    }

    public virtual void OnEnter()
    {

    }

    public virtual void OnExit()
    {

    }

    public virtual void UpdateTick()
    {

    }
}

public enum CharacterState : byte
{
    Disabled = 0,
    Grounded = 10,
    Airborne = 20
}

public enum lookDirectionOptions { velocity, acceleration, moveInput };
/// <summary>
/// A floating-capsule oriented physics based character controller. Based on the approach devised by Toyful Games for Very Very Valet.
/// </summary>
public class PhysicsBasedCharacterController : NetworkBehaviour
{
    public AnimationWrapper AnimWrapper => wrapper;
    [SerializeField] AnimationWrapper wrapper;
    public Oscillator SquashAndStretch => _squashAndStretchOcillator;
    public Rigidbody Rb => _rb;
    public Vector2 MoveContext => _moveContext;
    public WalkingOnWall Wall = WalkingOnWall.South;
    public CharacterState State = CharacterState.Grounded;
    StateMachine machine;
    public float Gravity = 9.8f;
    private Rigidbody _rb;

    private Vector2 _moveContext;
    private ParticleSystem.EmissionModule _emission;

    public LayerMask Terrain => _terrainLayer;
    public bool FaceCamera => _adjustInputsToCameraAngle;
    [Header("Other:")]
    [SerializeField] private bool _adjustInputsToCameraAngle = false;
    [SerializeField] private LayerMask _terrainLayer;
    [SerializeField] private ParticleSystem _dustParticleSystem;

    public RigidPlatform Platform => platform;
    RigidPlatform platform = null;
    public float RideHeight => _rideHeight;
    public float RayToGroundLength => _rayToGroundLength;
    public float RideSpringStrength => _rideSpringStrength;
    public float RideSpringDamper => _rideSpringDamper;
    [Header("Height Spring:")]
    [SerializeField] private float _rideHeight = 1.75f; // rideHeight: desired distance to ground (Note, this is distance from the original raycast position (currently centre of transform)). 
    [SerializeField] private float _rayToGroundLength = 3f; // rayToGroundLength: max distance of raycast to ground (Note, this should be greater than the rideHeight).
    [SerializeField] public float _rideSpringStrength = 50f; // rideSpringStrength: strength of spring. (?)
    [SerializeField] private float _rideSpringDamper = 5f; // rideSpringDampener: dampener of spring. (?)
    [SerializeField] private Oscillator _squashAndStretchOcillator;


    public float UpRightSpringStrength => _uprightSpringStrength;
    public float UpRightSpringDamper => _uprightSpringDamper;
    public lookDirectionOptions LookDirection => _characterLookDirection;
    [Header("Upright Spring:")]
    [SerializeField] private lookDirectionOptions _characterLookDirection = lookDirectionOptions.velocity;
    [SerializeField] private float _uprightSpringStrength = 40f;
    [SerializeField] private float _uprightSpringDamper = 5f;


    public float SpeedFactor => _speedFactor;
    public float MaxAccelForceFactor => _maxAccelForceFactor;

    private Vector3 _moveInput;
    private float _speedFactor = 1f;
    private float _maxAccelForceFactor = 1f;
    private Vector3 _m_GoalVel = Vector3.zero;

    public float MaxSpeed => _maxSpeed;
    public float Acceleration => _acceleration;
    public float MaxAccelForce => _maxAccelForce;
    public AnimationCurve AccelerationFactorFromDot => _accelerationFactorFromDot;
    public AnimationCurve MaxAccelerationForceFactorFromDot => _maxAccelerationForceFactorFromDot;
    public float LeanFactor => _leanFactor;

    [Header("Movement:")]
    [SerializeField] private float _maxSpeed = 8f;
    [SerializeField] private float _acceleration = 200f;
    [SerializeField] private float _maxAccelForce = 150f;
    [SerializeField] private float _leanFactor = 0.25f;
    [SerializeField] private AnimationCurve _accelerationFactorFromDot;
    [SerializeField] private AnimationCurve _maxAccelerationForceFactorFromDot;
    [SerializeField] private Vector3 _moveForceScale = new Vector3(1f, 0f, 1f);

    public bool Grounded = false;
    public Vector3 JumpInput => _jumpInput;
    private Vector3 _jumpInput;
    public float _timeSinceJumpPressed = 0f;
    public float _timeSinceUngrounded = 0f;
    public float _timeSinceJump = 0f;
    public bool _jumpReady = true;
    public bool _isJumping = false;
    public bool _prevGrounded = false;

    public float FallGravityFactor => _fallGravityFactor;
    public float RiseGravityFactor => _riseGravityFactor;
    public float LowJumpFactor => _lowJumpFactor;
    public float JumpBuffer => _jumpBuffer;
    public float CoyoteTime => _coyoteTime;
    public float JumpFactor => _jumpForceFactor;
    [Header("Jump:")]
    [SerializeField] private float _jumpForceFactor = 10f;
    [SerializeField] private float _riseGravityFactor = 5f;
    [SerializeField] private float _fallGravityFactor = 10f; // typically > 1f (i.e. 5f).
    [SerializeField] private float _lowJumpFactor = 2.5f;
    [SerializeField] private float _jumpBuffer = 0.15f; // Note, jumpBuffer shouldn't really exceed the time of the jump.
    [SerializeField] private float _coyoteTime = 0.25f;

    /// <summary>
    /// Prepare frequently used variables.
    /// </summary>
    /// 


    StateMachine clientmachine = new StateMachine();

    public void ChangeState(CharacterState state)
    {
        State = state;
        if (IsServer && !IsClient)
        {
            OnStateChangedClientRpc((byte)State);
        }
    }

    [ClientRpc]
    void OnStateChangedClientRpc(byte state)
    {
        ChangeState((CharacterState)state);
    }
    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        wrapper = GetComponentInChildren<AnimationWrapper>();
    }
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        //_gravitationalForce = -Helpers.UpRightDirection(Wall) * Gravity * _rb.mass;

        if (_dustParticleSystem)
        {
            _emission = _dustParticleSystem.emission; // Stores the module in a local variable
            _emission.enabled = false; // Applies the new value directly to the Particle System
        }
        machine = new StateMachine();

        CharacterStateControlled controlled = new CharacterStateControlled(this);
        Character disabled = new Character(this);
        CharacterJumpUp serverjump = new CharacterJumpUp(this);
        CharacterLand serverland = new CharacterLand(this);

        Func<bool> Controlled() => () => State == CharacterState.Grounded;
        Func<bool> Disabled() => () => State == CharacterState.Disabled;

        Func<bool> StartJump() => () => State == CharacterState.Airborne;
        Func<bool> EndJumpLand() => () => State == CharacterState.Grounded;

        machine.AddAnyTransition(controlled, Controlled());
        machine.AddAnyTransition(disabled, Disabled());
        machine.AddAnyTransition(serverjump, StartJump());
        machine.AddTransition(serverjump, serverland, EndJumpLand());


        controlled.OnJump += OnJumpClientRpc;


        clientmachine = new StateMachine();
        ClientCharacter client = new ClientCharacter(this);

        client.OnStartWalking += PlayWalkingParticles;
        client.OnStartWalking += PlayWalkingSFX;
        client.OnStartWalking += WalkingAnimation;

        client.OnStopWalking += IdleAnimation;
        client.OnStopWalking += StopWalkingSFX;
        client.OnStopWalking += StopParticlesVFX;

        client.OnLanded += PlayLandedSFX;//hmm need better way to detect. currently its server side..

        clientmachine.AddAnyTransition(client, Controlled());
        clientmachine.AddAnyTransition(disabled, Disabled());


    }

    void JumpAnimation()
    {
        wrapper.PlayAnimation("JumpUp");
    }
    void WalkingAnimation()
    {
        wrapper.PlayAnimation("Run");
    }
    void IdleAnimation()
    {
        wrapper.PlayAnimation("Idle");
    }



    [ClientRpc]
    void OnJumpClientRpc()
    {
        PlayJumpSFX();
        JumpAnimation();
        // _jumpReady = isReady;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
    }

    private void SetPlatform(RaycastHit rayHit)
    {
        rayHit.transform.TryGetComponent(out platform);
        if (platform != null)
        {
            RigidParent rigidParent = platform.rigidParent;
            GetComponent<NetworkObject>().TrySetParent(rigidParent.transform);
        }
        else
        {
            if (transform.parent != null)
            {
                GetComponent<NetworkObject>().TryRemoveParent();
            }
        }
    }
    public RaycastHit RayHit => rayHit;
    public bool RayHitGround => rayHitGround;
    public Ray RayToGround => rayToGround;

    RaycastHit rayHit;
    bool rayHitGround;
    Ray rayToGround;
    private void RaycastToGround()
    {
        rayHit = new RaycastHit();
        rayHitGround = false;
        rayToGround = new Ray(transform.position, -Helpers.UpRightDirection(Wall));
        rayHitGround = Physics.Raycast(rayToGround, out rayHit, RayToGroundLength, Terrain.value);
        Debug.DrawRay(transform.position, -Helpers.UpRightDirection(Wall) * RayToGroundLength, Color.blue);
    }
    public bool CheckGrounded()
    {
        RaycastToGround();
        if (rayHitGround)
        {
            SetPlatform(rayHit);
        }
        Grounded = CheckIfGrounded(rayHitGround, rayHit);
        return Grounded;
    }

    private bool CheckIfGrounded(bool rayHitGround, RaycastHit rayHit)
    {
        bool grounded;
        if (rayHitGround == true)
        {
            grounded = rayHit.distance <= RideHeight * 1.3f; // 1.3f allows for greater leniancy (as the value will oscillate about the rideHeight).
        }
        else
        {
            grounded = false;
        }
        return grounded;
    }

    private void Update()
    {
        machine.Tick();
        clientmachine.Tick();
    }

    /// <summary>
    /// Determines and plays the appropriate character sounds, particle effects, then calls the appropriate methods to move and float the character.
    /// </summary>
    private void FixedUpdate()
    {

        machine.FixedTick();

    
    }

    private void StopParticlesVFX()
    {
        if (_dustParticleSystem)
        {
            if (_emission.enabled == true)
            {
                _emission.enabled = false; // Applies the new value directly to the Particle System
            }
        }
    }

    private void StopWalkingSFX()
    {

        FindObjectOfType<AudioManager>().Stop("Walking");
      

    }

    void PlayJumpSFX()
    {
        FindObjectOfType<AudioManager>().Play("Jump");
    }
    private void PlayWalkingParticles()
    {

        if (_dustParticleSystem)
        {
            if (_emission.enabled == false)
            {
                _emission.enabled = true; // Applies the new value directly to the Particle System                  
            }
        }
    }

    private void PlayWalkingSFX()
    {

        if (!FindObjectOfType<AudioManager>().IsPlaying("Walking"))
        {
            FindObjectOfType<AudioManager>().Play("Walking");
        }
    }

    private void PlayLandedSFX()
    {
        if (!FindObjectOfType<AudioManager>().IsPlaying("Land"))
        {
            FindObjectOfType<AudioManager>().Play("Land");
        }
    }

   

   
    /// <summary>
    /// Reads the player movement input.
    /// </summary>
    /// <param name="context">The move input's context.</param>
    public void MoveInputAction(InputAction.CallbackContext context)
    {
        if (IsLocalPlayer)
        {
            _moveContext = context.ReadValue<Vector2>();
        }
      
        if (IsLocalPlayer && !IsServer)
        {
            SetInputServerRPC(_moveContext);
        }
       
    }
    [ServerRpc(RequireOwnership =false)]
    void SetInputServerRPC(Vector2 input)
    {
        _moveContext = input;
    }

    [ServerRpc(RequireOwnership = false)]
    void SetJumpServerRpc(Vector2 input)
    {
        _jumpInput = input;
      
    }

  
    /// <summary>
    /// Reads the player jump input.
    /// </summary>
    /// <param name="context">The jump input's context.</param>
    public void JumpInputAction(InputAction.CallbackContext context)
    {
        if (!IsLocalPlayer) return;
        float jumpContext = context.ReadValue<float>();
        _jumpInput = new Vector3(0, jumpContext, 0);

     
        //  Debug.Log(_jumpInput);
        if (context.started) // button down
        {
            _timeSinceJumpPressed = 0f;


        }

        if (!IsServer)
        {
            SetJumpServerRpc(_jumpInput);
        }
      //  ClientJumpFX();
     


    }

    void ClientJumpFX()
    {
        
        if (IsLocalPlayer)
        {
            if (_jumpReady && _jumpInput.y > 0)
            {
                PlayJumpSFX();
            }
        }
    }

   


}
