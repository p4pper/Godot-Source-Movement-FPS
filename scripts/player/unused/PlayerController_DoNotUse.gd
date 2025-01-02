extends CharacterBody3D

@export var lookSensitvity: float = 6
@export var jumpVelocity := 6.0
@export var walkSpeed := 7.0
@export var stealthSpeed := 5.0

var groundAcceleration := 16.0
var groundDecceleration := 10.0
var groundFriction := 3.0

@export var autoBhop : bool = false

# Sweet spot values for source-like air movement
var airCap = 0.05
var airAcceleration = 800.0
var airMoveSpeed = 500.0

const HEADBOB_MOVE_AMOUNT := 0.06
const HEADBOB_FREQUENCY := 2.4
var headbob_time := 0.0

var playerDir := Vector3.ZERO

var changingVars : Label
var constantVars : Label

func getMovementSpeed() -> float:
	if Input.is_action_pressed("stealth"):
		return stealthSpeed
	return walkSpeed

func _ready():
	
	changingVars = get_parent().get_node("CanvasLayer/PlayerUI/ChangingVars")
	constantVars = get_parent().get_node("CanvasLayer/PlayerUI/ConstantVars")
	lookSensitvity = lookSensitvity / 1000 # Making the configuration sens easy to read and change
	for child in %PlayerModel.find_children("*", "VisualInstance3D"):
		child.set_layer_mask_value(1, false)
		child.set_layer_mask_value(2, true)
	
# Need to change this to unhandled input and figure out how to ignore UIO
func _input(event):
	if event is InputEventMouseButton:
		Input.set_mouse_mode(Input.MOUSE_MODE_CAPTURED)
	elif event.is_action_pressed("ui_cancel"):
		Input.set_mouse_mode(Input.MOUSE_MODE_VISIBLE)
		
	if Input.get_mouse_mode() == Input.MOUSE_MODE_CAPTURED:
		if event is InputEventMouseMotion:
			rotateAround(event)

func rotateAround(event) -> void :
	rotate_y(-event.relative.x * lookSensitvity)
	%PlayerCamera.rotate_x(-event.relative.y * lookSensitvity)
	%PlayerCamera.rotation.x = clamp(%PlayerCamera.rotation.x, deg_to_rad(-90), deg_to_rad(90))

func _process(delta):
	pass
	
	
func _physics_process(delta):
	var inputDirection = Input.get_vector("left", "right", "forward", "backward").normalized()
	playerDir = self.global_transform.basis * Vector3(inputDirection.x, 0., inputDirection.y)
	
	if is_on_floor():
		if Input.is_action_just_pressed("jump") or(autoBhop and Input.is_action_pressed("jump")):
			self.velocity.y = jumpVelocity
		_handle_ground_physics(delta)
	else:
		_handle_air_physics(delta)
		
	move_and_slide()
	
	changingVars.text = \
	"CURRENT VARS" + "\n" + \
	"Player Speed: " + str(velocity.length()) + "\n" + \
	"Is Grounded: " + str(is_on_floor())
	
	#constantVars.text = \
	#"lookSensitvity: " + str(lookSensitvity) + "\n" + \
	#"jumpVelocity: " + str(jumpVelocity) + "\n" + \
	#"walkSpeed: " + str(walkSpeed) + "\n" + \
	#"stealthSpeed: " + str(stealthSpeed) + "\n" + \
	#"groundAcceleration: " + str(groundAcceleration) + "\n" + \
	#"groundDecceleration: " + str(groundDecceleration) + "\n" + \
	#"groundFriction: " + str(groundFriction) + "\n" + \
	#"autoBhop: " + str(autoBhop) + "\n" + \
	#"airCap: " + str(airCap) + "\n" + \
	#"airAcceleration: " + str(airAcceleration) + "\n" + \
	#"airMoveSpeed: " + str(airMoveSpeed)
func _headbob_effect(delta):
	headbob_time += delta * self.velocity.length()
	%PlayerCamera.transform.origin = Vector3(
		cos(headbob_time * HEADBOB_FREQUENCY * 0.5) * HEADBOB_MOVE_AMOUNT,
		sin(headbob_time * HEADBOB_FREQUENCY) * HEADBOB_MOVE_AMOUNT,
		0
	)
	
func _handle_air_physics(delta) -> void:
	self.velocity.y -= ProjectSettings.get_setting("physics/3d/default_gravity") * delta
	
	# Handle Air Physics using Quake/Source-like
	var currentSpeedInPlayerDir = self.velocity.dot(playerDir)
	var cappedSpeed = min((airMoveSpeed * playerDir).length(), airCap )
	var addSpeedTillCap = cappedSpeed - currentSpeedInPlayerDir
	
	if addSpeedTillCap > 0:
		var accelerationSpeed = airAcceleration * airMoveSpeed * delta
		accelerationSpeed = min(accelerationSpeed, addSpeedTillCap)
		self.velocity += accelerationSpeed * playerDir
	
	
func _handle_ground_physics(delta) -> void:
	var currentSpeedInPlayerDir = self.velocity.dot(playerDir)
	var addSpeedTillCap = getMovementSpeed() - currentSpeedInPlayerDir
	if(addSpeedTillCap > 0):
		var accelerationSpeed = groundAcceleration * delta * getMovementSpeed()
		accelerationSpeed = min(accelerationSpeed, addSpeedTillCap)
		self.velocity += accelerationSpeed * playerDir
	
	# Friction
	var control = max(self.velocity.length(), groundDecceleration)
	var drop = control * groundFriction * delta
	var newSpeed = max(self.velocity.length() - drop, 0.0)
	if self.velocity.length() > 0:
		newSpeed /= self.velocity.length()
	self.velocity *= newSpeed
	
	_headbob_effect(delta)
	
