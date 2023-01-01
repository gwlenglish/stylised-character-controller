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

public class CharacterStateControlled : Character
{
    public event Action OnLanded;
    public event Action OnWalking;
    public event Action OnStopWalking;
    Vector3 _gravitationalForce;
    Vector3 _moveInput;
    Vector3 _moveForceScale;
    Vector3 _m_GoalVel;
    RigidPlatform platform = null;
    private bool didLastRayHit;
    private Quaternion _uprightTargetRot = Quaternion.identity;
    private Quaternion _lastTargetRot = Quaternion.identity;
    private Vector3 _platformInitRot = Vector3.zero;
    private bool _prevGrounded;
    private Vector3 _previousVelocity;
    private float _timeSinceUngrounded;
    private float _timeSinceJump;
    private bool _isJumping;
    private bool _shouldMaintainHeight;
    private bool _jumpReady;
    private float _timeSinceJumpPressed;

    public CharacterStateControlled(PhysicsBasedCharacterController cc) : base(cc)
    {

    }

    private void CharacterJump(Vector3 jumpInput, bool grounded, RaycastHit rayHit)
    {
        _timeSinceJumpPressed += Time.fixedDeltaTime;
        _timeSinceJump += Time.fixedDeltaTime;
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
        if (fallingvel <= 0)//this is is
        {
            _shouldMaintainHeight = true;
            _jumpReady = true;
            if (!grounded)
            {
                // Increase downforce for a sudden plummet.
                cc.Rb.AddForce(_gravitationalForce * (cc.FallGravityFactor - 1f)); // Hmm... this feels a bit weird. I want a reactive jump, but I don't want it to dive all the time...
            }
        }
        else if (fallingvel > 0)
        {
            if (!grounded)
            {
                if (_isJumping)
                {
                    cc.Rb.AddForce(_gravitationalForce * (cc.RiseGravityFactor - 1f));
                }
                if (jumpInput == Vector3.zero)
                {
                    // Impede the jump height to achieve a low jump.
                    cc.Rb.AddForce(_gravitationalForce * (cc.LowJumpFactor - 1f));
                }
            }
        }

        if (_timeSinceJumpPressed < cc.JumpBuffer)
        {
            if (_timeSinceUngrounded < cc.CoyoteTime)
            {
                if (_jumpReady)
                {
                    _jumpReady = false;
                    _shouldMaintainHeight = false;
                    _isJumping = true;
                    cc.Rb.velocity = Vector3.Scale(cc.Rb.velocity, _moveForceScale);
                    // _rb.velocity = new Vector3(_rb.velocity.x, 0f, _rb.velocity.z); // Cheat fix... (see comment below when adding force to rigidbody).
                    if (rayHit.distance != 0) // i.e. if the ray has hit
                    {
                        //   _rb.position = new Vector3(_rb.position.x, _rb.position.y - (rayHit.distance - _rideHeight), _rb.position.z);
                    }
                    cc.Rb.AddForce(Helpers.UpRightDirection(cc.Wall) * cc.JumpFactor, ForceMode.Impulse); // This does not work very consistently... Jump height is affected by initial y velocity and y position relative to RideHeight... Want to adopt a fancier approach (more like PlayerMovement). A cheat fix to ensure consistency has been issued above...
                    //_timeSinceJumpPressed = _jumpBuffer; // So as to not activate further jumps, in the case that the player lands before the jump timer surpasses the buffer.
                    _timeSinceJump = 0f;

                    // FindObjectOfType<AudioManager>().Play("Jump");
                }
            }
        }
    }

    private void SetPlatform(RaycastHit rayHit)
    {
        rayHit.transform.TryGetComponent(out platform);
        if (platform != null)
        {
            RigidParent rigidParent = platform.rigidParent;
            cc.GetComponent<NetworkObject>().TrySetParent(rigidParent.transform);
        }
        else
        {
            if (cc.transform.parent != null)
            {
                cc.GetComponent<NetworkObject>().TryRemoveParent();
            }
        }
    }



    private void CalculateTargetRotation(Vector3 yLookAt, RaycastHit rayHit = new RaycastHit())
    {
        if (didLastRayHit)
        {
            _lastTargetRot = _uprightTargetRot;
            if (platform != null)
            {
                _platformInitRot = platform.transform.rotation.eulerAngles;
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
            if (platform != null)
            {
                _platformInitRot = platform.transform.rotation.eulerAngles;
            }
            else
            {
                _platformInitRot = Vector3.zero;
            }

        }
        else
        {
            if (platform != null)
            {
                Vector3 platformRot = platform.transform.rotation.eulerAngles;//get the rotation here that's not always y
                Vector3 deltaPlatformRot = platformRot - _platformInitRot;
                float yAngle = _lastTargetRot.eulerAngles.y + deltaPlatformRot.y;
                _uprightTargetRot = Quaternion.Euler(new Vector3(0f, yAngle, 0f));
                switch (platform.Wall)
                {
                    case WalkingOnWall.North:
                        _uprightTargetRot = Quaternion.LookRotation(platform.transform.forward, Helpers.UpRightDirection(platform.Wall));
                        break;
                    case WalkingOnWall.South:

                        _uprightTargetRot = Quaternion.LookRotation(platform.transform.forward, Helpers.UpRightDirection(platform.Wall));
                        break;
                    case WalkingOnWall.East:

                        _uprightTargetRot = Quaternion.LookRotation(platform.transform.right, Helpers.UpRightDirection(platform.Wall));
                        break;
                    case WalkingOnWall.West:

                        _uprightTargetRot = Quaternion.LookRotation(platform.transform.right, Helpers.UpRightDirection(platform.Wall));
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
    private (bool, RaycastHit) RaycastToGround()
    {
        RaycastHit rayHit;
        Ray rayToGround = new Ray(cc.transform.position, -Helpers.UpRightDirection(cc.Wall));
        bool rayHitGround = Physics.Raycast(rayToGround, out rayHit, cc.RayToGroundLength, cc.Terrain.value);
        Debug.DrawRay(cc.transform.position, -Helpers.UpRightDirection(cc.Wall) * cc.RayToGroundLength, Color.blue);
        return (rayHitGround, rayHit);
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
    private bool CheckIfGrounded(bool rayHitGround, RaycastHit rayHit)
    {
        bool grounded;
        if (rayHitGround == true)
        {
            grounded = rayHit.distance <= cc.RideHeight * 1.3f; // 1.3f allows for greater leniancy (as the value will oscillate about the rideHeight).
        }
        else
        {
            grounded = false;
        }
        return grounded;
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

        (bool rayHitGround, RaycastHit rayHit) = RaycastToGround();
        if (rayHitGround)
        {
            SetPlatform(rayHit);
        }


        bool grounded = CheckIfGrounded(rayHitGround, rayHit);
        if (grounded == true)
        {
            if (_prevGrounded == false)
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


            _timeSinceUngrounded = 0f;


        }
        else
        {
            OnStopWalking?.Invoke();

            _timeSinceUngrounded += Time.fixedDeltaTime;
        }

        CharacterMove(_moveInput, rayHit);
        CharacterJump(cc.JumpInput, grounded, rayHit);
        if (rayHitGround && _shouldMaintainHeight)
        {
            MaintainHeight(rayHit);
        }

        Vector3 lookDirection = GetLookDirection(cc.LookDirection);
        if (rayHitGround)
        {
            MaintainUpright(lookDirection, rayHit);
        }
       

        _prevGrounded = grounded;
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
    Controlled = 10
}

public enum lookDirectionOptions { velocity, acceleration, moveInput };
/// <summary>
/// A floating-capsule oriented physics based character controller. Based on the approach devised by Toyful Games for Very Very Valet.
/// </summary>
public class PhysicsBasedCharacterController : NetworkBehaviour
{
    public Oscillator SquashAndStretch => _squashAndStretchOcillator;
    public Rigidbody Rb => _rb;
    public Vector2 MoveContext => _moveContext;
    public WalkingOnWall Wall = WalkingOnWall.South;
    public CharacterState State = CharacterState.Controlled;
    StateMachine machine;
    public float Gravity = 9.8f;
    private Rigidbody _rb;
    private Vector3 _gravitationalForce;

    private Vector3 _previousVelocity = Vector3.zero;
    private Vector2 _moveContext;
    private ParticleSystem.EmissionModule _emission;

    public LayerMask Terrain => _terrainLayer;
    public bool FaceCamera => _adjustInputsToCameraAngle;
    [Header("Other:")]
    [SerializeField] private bool _adjustInputsToCameraAngle = false;
    [SerializeField] private LayerMask _terrainLayer;
    [SerializeField] private ParticleSystem _dustParticleSystem;

    private bool _shouldMaintainHeight = true;

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



    private Quaternion _uprightTargetRot = Quaternion.identity; // Adjust y value to match the desired direction to face.
    private Quaternion _lastTargetRot;
    private Vector3 _platformInitRot;
    private bool didLastRayHit;

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

    public Vector3 JumpInput => _jumpInput;
    private Vector3 _jumpInput;
    private float _timeSinceJumpPressed = 0f;
    private float _timeSinceUngrounded = 0f;
    private float _timeSinceJump = 0f;
    private bool _jumpReady = true;
    private bool _isJumping = false;
    private bool _prevGrounded = false;

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

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        _rb = GetComponent<Rigidbody>();
        _gravitationalForce = -Helpers.UpRightDirection(Wall) * Gravity * _rb.mass;

        if (_dustParticleSystem)
        {
            _emission = _dustParticleSystem.emission; // Stores the module in a local variable
            _emission.enabled = false; // Applies the new value directly to the Particle System
        }
        machine = new StateMachine();

        CharacterStateControlled controlled = new CharacterStateControlled(this);
        Character disabled = new Character(this);

        Func<bool> Controlled() => () => State == CharacterState.Controlled;
        Func<bool> Disabled() => () => State == CharacterState.Disabled;
        machine.AddAnyTransition(controlled, Controlled());
        machine.AddAnyTransition(disabled, Disabled());

        controlled.OnLanded += PlayLandedSFX;
        controlled.OnWalking += PlayWalkingParticles;
        controlled.OnWalking += PlayWalkingSFX;
        controlled.OnStopWalking += StopWalkingSFX;
        controlled.OnStopWalking += StopParticlesVFX;
      
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
    }

   

    private void Update()
    {
        machine.Tick();
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
        _moveContext = context.ReadValue<Vector2>();
        if (IsLocalPlayer && !IsServer)
        {
            SetInputServerRPC(_moveContext);
        }
        if (IsLocalPlayer)
        {
            if (_moveContext.magnitude > 0)
            {
                PlayWalkingParticles();
                PlayWalkingSFX();
            }
            else
            {
                StopParticlesVFX();
                StopWalkingSFX();
            }

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
        float jumpContext = context.ReadValue<float>();
        _jumpInput = new Vector3(0, jumpContext, 0);

        if (context.started) // button down
        {
            _timeSinceJumpPressed = 0f;
        }

        if (IsLocalPlayer && !IsServer)
        {
            SetJumpServerRpc(_jumpInput);
        }
    }

   


}
