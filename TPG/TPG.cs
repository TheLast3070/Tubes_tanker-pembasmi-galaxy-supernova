using System;
using System.Drawing;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;

public class TPG : Bot
{
    private int spinDirection = 1; 

    private const double RangeChase = 600; 
    private const double RangeClose = 150; 

    private double enemyX    = 0;
    private double enemyY    = 0;
    private double enemyDist = 0;
    private bool   hasEnemy  = false;

    private enum Phase { Patrol, Chase }
    private Phase phase = Phase.Patrol;

    private int       lostCounter = 0;
    private const int LostLimit   = 4;

    static void Main(string[] args) => new TPG().Start();
    TPG() : base(BotInfo.FromFile("TPG.json")) { }

    public override void Run()
    {
        BodyColor   = Color.Black;
        TurretColor = Color.Black;
        RadarColor  = Color.Black;
        BulletColor = Color.White;
        ScanColor   = Color.Crimson;

        AdjustGunForBodyTurn  = false;
        AdjustRadarForGunTurn = false;

        while (IsRunning)
        {
            if (hasEnemy)
            {
                lostCounter = LostLimit;
                hasEnemy    = false;
            }
            else
            {
                if (lostCounter > 0) lostCounter--;
                if (lostCounter == 0) phase = Phase.Patrol;
            }

            switch (phase)
            {
                case Phase.Patrol: Patrol(); break;
                case Phase.Chase:  chase();  break;
            }
            Go();
        }
    }

    private void Patrol()
    {
        SetTurnLeft(10_000 * spinDirection);
        MaxSpeed = 5;
        SetForward(10_000);
    }

    private void chase()
    {
        if (IsNearWall())
        {
            double bearingCenter = BearingTo(ArenaWidth / 2.0, ArenaHeight / 2.0);
            TurnLeft(bearingCenter);
            MaxSpeed = 6;
            SetForward(100);
            return;
        }
        double bearing = BearingTo(enemyX, enemyY);
        spinDirection = bearing >= 0 ? 1 : -1;
        TurnLeft(bearing);

        MaxSpeed = 8;
        SetForward(enemyDist + 5);
    }

    private const double WallMargin = 60;
    private bool IsNearWall() =>
        X < WallMargin || Y < WallMargin ||
        X > ArenaWidth  - WallMargin ||
        Y > ArenaHeight - WallMargin;

    public override void OnScannedBot(ScannedBotEvent e)
    {
        enemyX    = e.X;
        enemyY    = e.Y;
        enemyDist = DistanceTo(e.X, e.Y);

        if (enemyDist > RangeChase)
        {
            hasEnemy = false;
            Console.WriteLine($"Musuh tidak ditemukan lanjut mencari");
            return;
        }

        hasEnemy = true;
        Console.WriteLine($"Musuh ditemukan, KEJAR");
        phase    = Phase.Chase;

        if (GunHeat == 0)
        {
            if      (enemyDist < RangeClose) Fire(3);
            else if (enemyDist < 250)        Fire(2);
            else                             Fire(1.5);
        }
    }

    public override void OnHitBot(HitBotEvent e)
    {
        enemyX    = e.X;
        enemyY    = e.Y;
        enemyDist = DistanceTo(e.X, e.Y);
        hasEnemy  = true;
        phase     = Phase.Chase;
        Console.WriteLine($"Menabrak MUSUH");

        if      (e.Energy > 16) Fire(3);
        else if (e.Energy > 10) Fire(2);
        else if (e.Energy > 4)  Fire(1);
        else if (e.Energy > 2)  Fire(.5);
        else if (e.Energy > .4) Fire(.1);
    }
    public override void OnHitWall(HitWallEvent e)
    {
        Console.WriteLine($"Aduh Nabrak Tembok");
        spinDirection *= -1;
        Back(40);
        double bearingCenter = BearingTo(ArenaWidth / 2.0, ArenaHeight / 2.0);
        TurnLeft(bearingCenter);
        SetForward(80);
    }
}
