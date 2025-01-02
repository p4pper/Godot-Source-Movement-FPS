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
    [Export] private Label debugLabel;

    [Export] private bool crouchIsToggleable = false; // If this is false, crouch is done by holding.

    [Export] private CameraAttributesPractical cameraEnvSettings;
    [Export] private Control pauseUI;

    private float originalWalkSpeed;
    private float stealthSpeed;
    private bool isCrouching = false;

    private const float HEADBOB_MOVE_AMOUNT = 0.06f;
    private const float HEADBOB_FREQUENCY = 2.4f;

    private float groundAcceleration = 16f;
    private float groundDecceleration = 10f;
    private float groundFriction = 3f;

    // Affects movement in the air
    private float airCap = 0.5f;
    private float airAcceleration = 800f;
    private float airMovementSpeed = 500f;

    private float headbobTime = 0.0f;
    private Vector3 playerDirection = Vector3.Zero;
    private Camera3D playerCamera;
    

    public override void _Ready()
    {
        originalWalkSpeed = walkSpeed;
        UpdateSpeeds();
        HidePlayerModelFromCamera();
        InitializeCamera();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        HandlePauseAndMouseInput(@event);
        HandleCameraRotation(@event);
        HandleCrouchInput(@event);
    }

    public override void _Process(double delta)
    {
                debugLabel.Text = @$"
                FPS: {Math.Round(1 / delta)} | Frame Time: {Math.Round(delta * 1000)} ms
                Position: X: {Math.Round(Position.X)}, Y: {Math.Round(Position.Y)}, Z: {Math.Round(Position.Z)}
                Speed: {Math.Round(Velocity.Length())}
                On Floor: {IsOnFloor()}
                ";
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Input.MouseMode == Input.MouseModeEnum.Captured)
            UpdatePlayerDirection();

        if (IsOnFloor())
            ProcessGroundMovement((float)delta);
        else
            ProcessAirMovement((float)delta);

        MoveAndSlide();
    }

    private void HidePlayerModelFromCamera()
    {
        foreach (VisualInstance3D child in GetNode<Node3D>("PlayerModel").FindChildren("*", "VisualInstance3D"))
        {
            child.SetLayerMaskValue(1, false);
            child.SetLayerMaskValue(2, true);
        }
    }

    private void InitializeCamera()
    {
        playerCamera = GetNode<Camera3D>("%PlayerCamera");
    }

    private void HandlePauseAndMouseInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton)
            Input.MouseMode = Input.MouseModeEnum.Captured;

        if (@event.IsActionPressed("ui_cancel"))
        {
            TogglePauseUI();
        }
    }

    private void TogglePauseUI()
    {
        bool isPaused = Input.MouseMode == Input.MouseModeEnum.Visible;
        pauseUI.Visible = !isPaused;
        cameraEnvSettings.DofBlurFarEnabled = !isPaused;
        Input.MouseMode = isPaused ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
    }

    private void HandleCameraRotation(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            RotateY(-motion.Relative.X * lookSensitivity);
            playerCamera.RotateX(-motion.Relative.Y * lookSensitivity);
            playerCamera.Rotation = playerCamera.Rotation with { X = Math.Clamp(playerCamera.Rotation.X, Mathf.DegToRad(-90f), Mathf.DegToRad(90f)) };
        }
    }

    private void HandleCrouchInput(InputEvent @event)
    {
        if (@event.IsActionPressed("crouch") && crouchIsToggleable)
            ToggleCrouch();


        if(!crouchIsToggleable)
        {
            if (@event.IsActionPressed("crouch"))
            {
                HoldCrouch(true);
            } else if (@event.IsActionReleased("crouch"))
            {
                HoldCrouch(false);
            }
        }

    }

    private void UpdatePlayerDirection()
    {
        Vector2 inputDirection = Input.GetVector("left", "right", "forward", "backward").Normalized();
        playerDirection = GlobalTransform.Basis * new Vector3(inputDirection.X, 0f, inputDirection.Y);
    }

    private void ProcessGroundMovement(float delta)
    {
        if (Input.IsActionJustPressed("jump") || (allowAutoJump && Input.IsActionPressed("jump")))
        {
            // Preserve horizontal velocity while applying the vertical jump force
            Velocity = new Vector3(Velocity.X, jumpForce, Velocity.Z);
        }

        ApplyGroundPhysics(delta);

    }

    private void ProcessAirMovement(float delta)
    {
        ApplyGravity(delta);
        ApplyAirPhysics(delta);
    }

private void ApplyGroundPhysics(float delta)
{
    // Apply acceleration and friction
    ApplyAcceleration(delta, groundAcceleration, getMovementSpeed());
    ApplyFriction(delta);

    // Limit speed only when grounded
    if (IsOnFloor())
    {
        float maxSpeed = getMovementSpeed();
        Vector3 velocityDirection = Velocity.Normalized();

        // Handle wall sliding speed
        if (IsOnWall())
        {
            Vector3 wallNormal = GetWallNormal();
            Vector3 slideDirection = Velocity.Slide(wallNormal);

            if (slideDirection.Length() > maxSpeed)
            {
                slideDirection = slideDirection.Normalized() * maxSpeed;
                Velocity = new Vector3(slideDirection.X, Velocity.Y, slideDirection.Z);
            }
        }
        else
        {
            // Clamp only horizontal speed
            Vector3 horizontalVelocity = new Vector3(Velocity.X, 0, Velocity.Z);
            if (horizontalVelocity.Length() > maxSpeed)
            {
                horizontalVelocity = horizontalVelocity.Normalized() * maxSpeed;
                Velocity = new Vector3(horizontalVelocity.X, Velocity.Y, horizontalVelocity.Z);
            }
        }
    }

    // Apply headbob effect only when moving
    if (Velocity.Length() > 1.4f)
        ApplyHeadbobEffect(delta);
}



    private void ApplyAirPhysics(float delta)
    {
        float maxSpeed = Mathf.Min(airMovementSpeed * playerDirection.Length(), airCap);
        ApplyAcceleration(delta, airAcceleration * airMovementSpeed, maxSpeed);
    }

    private void ApplyAcceleration(float delta, float acceleration, float maxSpeed)
    {
        float currentSpeed = Velocity.Dot(playerDirection);
        float addSpeed = maxSpeed - currentSpeed;
        if (addSpeed > 0)
        {
            float accelSpeed = Mathf.Min(acceleration * delta * maxSpeed, addSpeed);
            Velocity += accelSpeed * playerDirection;
        }
    }

    private void ApplyFriction(float delta)
    {
        float control = Mathf.Max(Velocity.Length(), groundDecceleration);
        float drop = control * groundFriction * delta;
        float newSpeed = Mathf.Max(Velocity.Length() - drop, 0.0f);

        if (Velocity.Length() > 0)
            newSpeed /= Velocity.Length();

        Velocity *= newSpeed;
    }

    private void ApplyGravity(float delta)
    {
        float gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
        Velocity += new Vector3(0, -gravity * delta, 0);
    }

    private void ApplyHeadbobEffect(float delta)
    {
        headbobTime += delta * Velocity.Length();
        playerCamera.Transform = playerCamera.Transform with { Origin = new Vector3(
            Mathf.Cos(headbobTime * HEADBOB_FREQUENCY * 0.5f) * HEADBOB_MOVE_AMOUNT,
            Mathf.Cos(headbobTime * HEADBOB_FREQUENCY) * HEADBOB_MOVE_AMOUNT,
            0f
        ) };
    }

    private void ToggleCrouch()
    {
            if (!IsOnFloor()) // Defer crouch toggle until player lands
            {
                return;
            }

            if (isCrouching)
            {
                animationPlayer.Play("playerCrouch", -1, -crouchSpeed, true); // Uncrouch
            }
            else
            {
                animationPlayer.Play("playerCrouch", -1, crouchSpeed); // Crouch
            }

            isCrouching = !isCrouching;
            UpdateSpeeds();
    }

    private void HoldCrouch(bool isHolding)
    {
            if (!IsOnFloor()) // Defer crouch toggle until player lands
            {
                return;
            }

            
            if (isHolding && !isCrouching) // Start crouching
            {
                animationPlayer.Play("playerCrouch", -1, crouchSpeed);
                isCrouching = true;
            }
            else if (!isHolding && isCrouching) // Stop crouching
            {
                animationPlayer.Play("playerCrouch", -1, -crouchSpeed, true);
                isCrouching = false;
            }
            
            UpdateSpeeds();
    }

    private void UpdateSpeeds()
    {
        walkSpeed = isCrouching ? originalWalkSpeed * 0.7f : originalWalkSpeed;
        stealthSpeed = walkSpeed * 0.7f;
    }

    private float getMovementSpeed() => Input.IsActionPressed("stealth") ? stealthSpeed : walkSpeed;
}
