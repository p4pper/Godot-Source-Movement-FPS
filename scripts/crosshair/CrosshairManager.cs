using Godot;
using System;

public partial class Reticle : CenterContainer
{

    [Export] private int clThickness = 2;
    [Export] private bool clDynamic = true;

    [Export] private Line2D[] reticleLines;
    [Export] private CharacterBody3D playerController;
    [Export] private float reticleSpeed = 0.25f;
    [Export] private float reticleDistance = 0.75f;

    [Export] private float dotRadius = 1.0f;
    [Export] private Color dotColor = Colors.White;

    public override void _Ready()
    {
        for (int i = 0; i < reticleLines.Length; i++)
        {

        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawCircle(new Vector2(20,20),dotRadius, dotColor);
    }

    public override void _Process(double delta)
    {
        if(clDynamic)
            adjustReticle();
    }

    private void adjustReticle()
    {
        Vector3 velocity = playerController.GetRealVelocity();
        Vector3 origin = new Vector3(20,20,0);
        Vector2 position = new Vector2(20, 20);
        float speed = velocity.Length();
    
        reticleLines[0].Position = reticleLines[0].Position.Lerp(
            position + new Vector2(0, -speed * reticleDistance),
            reticleSpeed
        );

        reticleLines[1].Position = reticleLines[1].Position.Lerp(
           position + new Vector2(speed * reticleDistance, 0),
           reticleSpeed
       );

        reticleLines[2].Position = reticleLines[2].Position.Lerp(
           position + new Vector2(0, speed * reticleDistance),
           reticleSpeed
       );

        reticleLines[3].Position = reticleLines[3].Position.Lerp(
           position + new Vector2(-speed * reticleDistance, 0),
           reticleSpeed
       );
    }
}
