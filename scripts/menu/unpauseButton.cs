using Godot;
using System;

public partial class unpauseButton : Node
{
    private void _on_pressed()
    {
        Input.ActionPress("ui_cancel", 1f);
        Input.ActionRelease("ui_cancel");
    }
}
