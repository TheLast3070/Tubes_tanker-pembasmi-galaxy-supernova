using System;
using System.Collections.Generic;
using System.Drawing;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;

public class Galactus : Bot
{
    private Dictionary<int, double> trackedBots = new Dictionary<int, double>();
    private const double DYING_THRESHOLD = 20.0;

    static void Main(string[] args) => new Galactus().Start();
    Galactus() : base(BotInfo.FromFile("Galactus.json")) { }

    public override void Run()
    {
        BodyColor   = Color.Black;
        TurretColor = Color.Blue;
        RadarColor  = Color.Red;

        while (IsRunning)
        {
            SetForward(150);
            SetTurnLeft(20);
            SetTurnRadarRight(360);
            Go();
        }
    }

    public override void OnScannedBot(ScannedBotEvent e)
    {
        trackedBots[e.ScannedBotId] = e.Energy;

        bool enemyIsDying = e.Energy <= DYING_THRESHOLD;

        double angleToEnemy = BearingTo(e.X, e.Y);

        // Hitung selisih sudut turret ke musuh
        double gunTurn = angleToEnemy - GunDirection;
        // Normalisasi ke rentang -180..180
        while (gunTurn >  180) gunTurn -= 360;
        while (gunTurn < -180) gunTurn += 360;

        SetTurnGunRight(gunTurn);

        double distance = DistanceTo(e.X, e.Y);

        if (GunHeat == 0)
        {
            if (enemyIsDying)
            {
                Fire(3.0);
                SetForward(distance > 150 ? distance - 100 : 0);
                SetTurnLeft(gunTurn);
            }
            else if (distance < 200)
            {
                Fire(3.0);
            }
            else
            {
                Fire(1.0);
            }
        }
    }

    private bool HasDyingTarget()
    {
        foreach (var entry in trackedBots)
            if (entry.Value <= DYING_THRESHOLD) return true;
        return false;
    }

    public override void OnBotDeath(BotDeathEvent e)
    {
        trackedBots.Remove(e.VictimId);
    }

    public override void OnHitWall(HitWallEvent e)
    {
        Back(100);
        SetTurnRight(90);
    }

    public override void OnHitBot(HitBotEvent e)
    {
        if (e.IsRammed) Forward(100);
        else            Back(100);
    }

    public override void OnHitByBullet(HitByBulletEvent e)
    {
        if (!HasDyingTarget())
        {
            SetTurnLeft(45);
            SetForward(150);
        }
    }
}
