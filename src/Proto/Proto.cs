using System;
using System.Drawing;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;

public class Proto : Bot
{
    private readonly Random rng = new();

    private const double WallMargin    = 100; 
    private const double OrbitRadius   = 250;  
    private const double MaxFirePower  = 3.0;
    private const double MinFirePower  = 0.5;
    private const double BulletSpeed   = 20;   

    private double enemyX       = -1;
    private double enemyY       = -1;
    private double enemyVX      = 0;  
    private double enemyVY      = 0; 
    private double enemyEnergy  = 100;
    private bool   hasEnemy     = false;

    private double prevEnemyX   = -1;
    private double prevEnemyY   = -1;

    private int    orbitDir     = 1;
    private int    orbitFlip    = 0;  

    private double prevEnemyEnergy = 100;

    static void Main(string[] args) => new Proto().Start();
    Proto() : base(BotInfo.FromFile("Proto.json")) { }

    public override void Run()
    {
        BodyColor   = Color.FromArgb(20, 20, 20);
        TurretColor = Color.FromArgb(200, 50, 50);
        RadarColor  = Color.FromArgb(50, 200, 255);
        BulletColor = Color.FromArgb(255, 200, 0);
        ScanColor   = Color.FromArgb(50, 200, 255);

        AdjustGunForBodyTurn  = false;
        AdjustRadarForGunTurn = false;

        while (IsRunning)
        {
            if (hasEnemy)
            {
                OrbitAndShoot();
            }
            else
            {
                SearchEnemy();
            }
        }
    }

    private void OrbitAndShoot()
    {
        double dist    = DistanceTo(enemyX, enemyY);
        double bearing = BearingTo(enemyX, enemyY); 

        orbitFlip--;
        if (orbitFlip <= 0)
        {
            orbitDir  = rng.Next(2) == 0 ? 1 : -1;
            orbitFlip = rng.Next(5, 15); 
        }

        double orbitAngle = bearing + (90.0 * orbitDir);

        double distError = dist - OrbitRadius;
        double distCorrection = Math.Max(-40, Math.Min(40, distError * 0.15));
        orbitAngle -= distCorrection * orbitDir;
        orbitAngle = SmoothAngle(orbitAngle);
        TurnRight(orbitAngle);
        Forward(rng.Next(80, 140));

        hasEnemy = false; 
    }

    private void SearchEnemy()
    {
        if (IsNearWall())
        {
            double b = BearingTo(ArenaWidth / 2.0, ArenaHeight / 2.0);
            TurnRight(b);
            Forward(120);
            return;
        }

        double toBearing = BearingTo(ArenaWidth / 2.0, ArenaHeight / 2.0);
        TurnRight(toBearing + rng.Next(-30, 31));
        Forward(rng.Next(60, 120));
    }

    private double SmoothAngle(double angle)
    {
        double absDir = NormalizeDeg(Direction + angle);
        double moveX  = X + Math.Sin(DegToRad(absDir)) * 100;
        double moveY  = Y + Math.Cos(DegToRad(absDir)) * 100;

        int maxIter = 20;
        while (maxIter-- > 0 &&
               (moveX < WallMargin || moveX > ArenaWidth - WallMargin ||
                moveY < WallMargin || moveY > ArenaHeight - WallMargin))
        {
            angle  += 15.0 * orbitDir;
            absDir  = NormalizeDeg(Direction + angle);
            moveX   = X + Math.Sin(DegToRad(absDir)) * 100;
            moveY   = Y + Math.Cos(DegToRad(absDir)) * 100;
        }

        return angle;
    }

    public override void OnScannedBot(ScannedBotEvent e)
    {

        double energyDrop = prevEnemyEnergy - e.Energy;
        if (energyDrop >= MinFirePower && energyDrop <= MaxFirePower)
        {
            orbitDir  = -orbitDir;
            orbitFlip = rng.Next(3, 8);
        }
        prevEnemyEnergy = e.Energy;

        if (prevEnemyX >= 0)
        {
            enemyVX = e.X - prevEnemyX;
            enemyVY = e.Y - prevEnemyY;
        }
        prevEnemyX = e.X;
        prevEnemyY = e.Y;

        enemyX      = e.X;
        enemyY      = e.Y;
        enemyEnergy = e.Energy;
        hasEnemy    = true;

        double dist = DistanceTo(e.X, e.Y);

        double power = CalcFirePower(dist);

        double ticksToHit  = dist / (BulletSpeed - 3 * power);
        double predictedX  = e.X + enemyVX * ticksToHit;
        double predictedY  = e.Y + enemyVY * ticksToHit;

        predictedX = Math.Max(0, Math.Min(ArenaWidth,  predictedX));
        predictedY = Math.Max(0, Math.Min(ArenaHeight, predictedY));

        double predictedBearing = BearingTo(predictedX, predictedY);
        if (Math.Abs(predictedBearing) > 2)
        {
            TurnRight(predictedBearing);
        }

        if (GunHeat == 0)
        {
            Fire(power);
        }
    }

    private double CalcFirePower(double dist)
    {
        double power;

        if      (dist < 150) power = 3.0;
        else if (dist < 280) power = 2.0;
        else if (dist < 400) power = 1.5;
        else                 power = 1.0;

        if (Energy < 30) power = Math.Min(power, 1.5);
        if (Energy < 15) power = Math.Min(power, 1.0);

        return Math.Max(MinFirePower, Math.Min(MaxFirePower, power));
    }

    public override void OnHitWall(HitWallEvent e)
    {
        Console.WriteLine($"Aduh Nabrak Tembok");
        Back(50);
        double bearing = BearingTo(ArenaWidth / 2.0, ArenaHeight / 2.0);
        TurnRight(bearing);
        Forward(100);
        hasEnemy = false;
    }

    public override void OnHitBot(HitBotEvent e)
    {
        Console.WriteLine($"Aduh Nabrak MUSUH");
        double bearing = BearingTo(e.X, e.Y);
        if (bearing > -10 && bearing < 10)
            Fire(3);
        Back(60);
        TurnRight(bearing > 0 ? -110 : 110);
        Forward(100);
    }
    private bool IsNearWall()
    {
        return X < WallMargin || Y < WallMargin ||
               X > ArenaWidth - WallMargin || Y > ArenaHeight - WallMargin;
    }

    private static double NormalizeDeg(double d)
    {
        d %= 360;
        if (d < 0) d += 360;
        return d;
    }

    private static double DegToRad(double d) => d * Math.PI / 180.0;
}
