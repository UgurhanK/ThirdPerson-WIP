using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Utils;
using System.Drawing;
using VectorSystem = System.Numerics;

namespace ThirdPerson;

public static class EntityUtilities
{
    static public void SetColor(this CDynamicProp? prop, Color colour)
    {
        if (prop != null && prop.IsValid)
        {
            prop.Render = colour;
            Utilities.SetStateChanged(prop, "CBaseModelEntity", "m_clrRender");
        }
    }

    static public void SetColor(this CPhysicsPropMultiplayer? prop, Color colour)
    {
        if (prop != null && prop.IsValid)
        {
            prop.Render = colour;
            Utilities.SetStateChanged(prop, "CBaseModelEntity", "m_clrRender");
        }
    }

    public static void UpdateCamera(this CDynamicProp _cameraProp, CCSPlayerController target)
    {
        _cameraProp.Teleport(target.CalculatePositionInFront(-110, 90), target.PlayerPawn.Value!.V_angle, new Vector());
    }

    public static void UpdateCameraSmooth(this CPhysicsPropMultiplayer _cameraProp, CCSPlayerController target)
    {
        Vector velocity = CalculateVelocity(_cameraProp.AbsOrigin!, target.CalculatePositionInFront(-110, 90), ThirdPerson.cfg.SmoothDuration);
        _cameraProp.Teleport(_cameraProp.AbsOrigin!, target.PlayerPawn.Value!.V_angle, velocity);
    }

    public static Vector CalculateVelocity(Vector positionA, Vector positionB, float timeDuration)
    {
        // Step 1: Determine direction from A to B
        Vector directionVector = positionB - positionA;

        // Step 2: Calculate distance between A and B
        float distance = directionVector.Length();

        // Step 3: Choose a desired time duration for the movement
        // Ensure that timeDuration is not zero to avoid division by zero
        if (timeDuration == 0)
        {
            timeDuration = 1;
        }

        // Step 4: Calculate velocity magnitude based on distance and time
        float velocityMagnitude = distance / timeDuration;

        // Step 5: Normalize direction vector
        if (distance != 0)
        {
            directionVector /= distance;
        }

        // Step 6: Scale direction vector by velocity magnitude to get velocity vector
        Vector velocityVector = directionVector * velocityMagnitude;

        return velocityVector;
    }

    public static Vector CalculatePositionInFront(this CCSPlayerController player, float offSetXY, float offSetZ = 0)
    {
        var pawn = player.PlayerPawn.Value;
        // Extract yaw angle from player's rotation QAngle
        float yawAngle = pawn!.EyeAngles!.Y;

        // Convert yaw angle from degrees to radians
        float yawAngleRadians = (float)(yawAngle * Math.PI / 180.0);

        // Calculate offsets in x and y directions
        float offsetX = offSetXY * (float)Math.Cos(yawAngleRadians);
        float offsetY = offSetXY * (float)Math.Sin(yawAngleRadians);

        // Calculate position in front of the player
        var positionInFront = new Vector
        {
            X = pawn!.AbsOrigin!.X + offsetX,
            Y = pawn!.AbsOrigin!.Y + offsetY,
            Z = pawn!.AbsOrigin!.Z + offSetZ
        };

        return positionInFront;
    }

    public static bool IsInfrontOfPlayer(this CCSPlayerController player1, CCSPlayerController player2)
    {
        var player1Pawn = player1.PlayerPawn.Value;
        var player2Pawn = player2.PlayerPawn.Value;
        var yawAngleRadians = (float)(player1Pawn!.EyeAngles.Y * Math.PI / 180.0);

        // Calculate the direction vector of player1 based on yaw angle
        Vector player1Direction = new(
            MathF.Cos(yawAngleRadians),
            MathF.Sin(yawAngleRadians),
            0
        );

        // Vector from player1 to player2
        Vector player1ToPlayer2 = player2Pawn!.AbsOrigin! - player1Pawn.AbsOrigin!;

        // Calculate dot product to determine if player2 is behind player1
        float dotProduct = player1ToPlayer2.Dot(player1Direction);

        // If the dot product is negative, player2 is behind player1
        return dotProduct < 0;
    }

    public static float Dot(this Vector vector1, Vector vector2)
    {
        return vector1.X * vector2.X + vector1.Y * vector2.Y + vector1.Z * vector2.Z;
    }

    static public void Health(this CCSPlayerController player, int health)
    {
        if (player.PlayerPawn == null || player.PlayerPawn.Value == null)
        {
            return;
        }

        player.Health = health;
        player.PlayerPawn.Value.Health = health;

        if (health > 100)
        {
            player.MaxHealth = health;
            player.PlayerPawn.Value.MaxHealth = health;
        }

        Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseEntity", "m_iHealth");
    }
}