using Godot;
using System;
using System.ComponentModel.DataAnnotations;

public partial class PlayerController : CharacterBody3D
{

    [Export] private AnimationPlayer animationPlayer;

    [Export, Range(0.0f, 1.0f)] private float crouchSpeed = 7f;
    [Export] private float lookSensitivity = 0.006f;
    [Export] private float jumpForce = 6f;
    [Export] private float walkSpeed = 7f;
    
    [Export] private bool allowAutoJump = false;

    private float originalWalkSpeed;
    private float stealthSpeed;

    private float groundAcceleration = 16f;
    private float groundDecceleration = 10f;
    private float groundFriction = 3f;
    private bool isCrouching = false;

    private float airCap = 0.05f;
    private float airAcceleration = 800f;
    private float airMovementSpeed = 500f;

    private const float HEADBOB_MOVE_AMOUNT = 0.06f;
    private const float HEADBOB_FREQUENCY = 2.4f;
    private float headbobTime = 0.0f;

    private Vector3 playerDirection = Vector3.Zero;
    private Label changingVariablesLabel;

    private Camera3D playerCamera;
    [Export] private CameraAttributesPractical cameraEnvSettings;
    [Export] private Control pauseUI;

    private bool waitForFloorToCrouch = false;

    public override void _Ready()
    {
        originalWalkSpeed = walkSpeed;
        setSpeeds();

        // Hide Player's model from player's camera
        foreach (VisualInstance3D child in GetNode<Node3D>("PlayerModel").FindChildren("*", "VisualInstance3D"))
        {
            child.SetLayerMaskValue(1, false);
            child.SetLayerMaskValue(2, true);
        }

        // Initilaise camera with unique node name.
        playerCamera = GetNode<Camera3D>("%PlayerCamera");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Capture mouse, or uncapture as ESC pressed
        if (@event is InputEventMouseButton)
            Input.MouseMode = Input.MouseModeEnum.Captured;
        else if (@event.IsActionPressed("ui_cancel"))
            if (Input.MouseMode == Input.MouseModeEnum.Captured)
            {
                pauseUI.Visible = true;
                cameraEnvSettings.DofBlurFarEnabled = true;
                Input.MouseMode = Input.MouseModeEnum.Visible;
            }
            else
            {
                pauseUI.Visible = false;
                cameraEnvSettings.DofBlurFarEnabled = false;
                Input.MouseMode = Input.MouseModeEnum.Captured;
            }
        if (@event is InputEventMouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            RotatePlayerCamera(@event as InputEventMouseMotion);
        }


        if(@event.IsActionPressed("crouch"))
        {
            toggleCrouch();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            Vector2 inputDirection = Input.GetVector("left", "right", "forward", "backward").Normalized();
            playerDirection = GlobalTransform.Basis * new Vector3(inputDirection.X, 0f, inputDirection.Y);
        }
        if (IsOnFloor())
        {
            if(Input.IsActionJustPressed("jump") || (allowAutoJump && Input.IsActionPressed("jump")))
            {
                Velocity = new Vector3(Velocity.X, jumpForce, Velocity.Z);
            }
            handleGroundPhysics((float)delta);
        } else
        {
            handleAirPhysics((float)delta);
        }

        if (waitForFloorToCrouch && IsOnFloor())
        {
            toggleCrouch();
            waitForFloorToCrouch = false;
        }

        MoveAndSlide();
    }

    private float getMovementSpeed()
    {
        setSpeeds();
        if (Input.IsActionPressed("stealth"))
            return stealthSpeed;
        return walkSpeed;
    }

    // Cast delta as (float) when calling those functions because delta is double in godot.
    private void handleGroundPhysics(float delta)
    {
        var currentSpeedInPlayerDir = Velocity.Dot(playerDirection);
        var addSpeedTillCap = getMovementSpeed() - currentSpeedInPlayerDir;
        if(addSpeedTillCap > 0)
        {
            var accelerationSpeed = groundAcceleration * delta * getMovementSpeed();
            accelerationSpeed = MathF.Min(accelerationSpeed, addSpeedTillCap);
            Velocity += accelerationSpeed * playerDirection;
        }

        // Friction
        var control = Mathf.Max(Velocity.Length(), groundDecceleration);
        var drop = control * groundFriction * delta;
        var newSpeed = Mathf.Max(Velocity.Length() - drop, 0.0);
        

        if(Velocity.Length() > 0)
        {
            newSpeed /= Velocity.Length();
        }
        Velocity = Velocity with { X=Velocity.X * (float)newSpeed, 
            Y=Velocity.Y * (float)newSpeed,
            Z=Velocity.Z * (float)newSpeed
        };

        headbobEffect(delta);
    }

    private void handleAirPhysics(float delta)
    {
        var gravity = ProjectSettings.GetSetting("physics/3d/default_gravity");
        Velocity = new Vector3(Velocity.X, Velocity.Y - (float)gravity * delta, Velocity.Z);

        var currentSpeedInPlayerDirection = Velocity.Dot(playerDirection);
        var cappedSpeed = Mathf.Min((airMovementSpeed * playerDirection).Length(), airCap);
        var addSpeedTillCap = cappedSpeed - currentSpeedInPlayerDirection;

        if (addSpeedTillCap > 0)
        {
            var accelerationSpeed = airAcceleration * airMovementSpeed * delta;
            accelerationSpeed = Mathf.Min(accelerationSpeed, addSpeedTillCap);
            Velocity += accelerationSpeed * playerDirection;
        }
    }

    private void headbobEffect(float delta)
    {
        headbobTime += delta * Velocity.Length();
        playerCamera.Transform = playerCamera.Transform with { Origin = new Vector3(
            Mathf.Cos(headbobTime * HEADBOB_FREQUENCY * 0.5f) * HEADBOB_MOVE_AMOUNT,
            Mathf.Cos(headbobTime * HEADBOB_FREQUENCY) * HEADBOB_MOVE_AMOUNT,
            0f
            ) 
        };
    }

    private void RotatePlayerCamera(InputEventMouseMotion @event)
    {
        RotateY(-@event.Relative.X * lookSensitivity);
        playerCamera.RotateX(-@event.Relative.Y * lookSensitivity);
        playerCamera.Rotation = playerCamera.Rotation with { X = Math.Clamp(playerCamera.Rotation.X, Mathf.DegToRad(-90f), Mathf.DegToRad(90f)) };
    }

    private void toggleCrouch()
    {
        if (isCrouching)
        {
            animationPlayer.Play("playerCrouch", -1, -crouchSpeed, true);
            isCrouching = !isCrouching;
        }
        else
        {
            if (this.IsOnFloor())
            {
                animationPlayer.Play("playerCrouch", -1, crouchSpeed);
                isCrouching = !isCrouching;
            } else
            {
                waitForFloorToCrouch = true;
            }
            
        }
    }

    private void setSpeeds()
    {
        if(isCrouching)
        {
            walkSpeed = originalWalkSpeed * 0.7f;
            stealthSpeed = walkSpeed * 0.5f;
        } else
        {
            walkSpeed = originalWalkSpeed;
            stealthSpeed = walkSpeed * 0.7f;
        }
    }
}
