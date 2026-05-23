using System;
using System.Drawing;
using System.Collections.Generic;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;

//  Prioritaskan bertahan hidup dengan menjaga jarak dan mengikuti dinding
// Fungsi Objektif: Maksimalkan Survival Score + Last Survival Bonus
public class BotTheLast : Bot
{
    static void Main(string[] args) => new BotTheLast().Start();
    BotTheLast() : base(BotInfo.FromFile("BotTheLast.json")) { }

    private class DataMusuh // Menyimpan informasi tentang musuh yang terdeteksi
    {
        public int    Id;
        public double PosisiX, PosisiY;
        public double Energi, Kecepatan, Arah;
        public int    TurnTerakhirTerdeteksi;
    }
    private Dictionary<int, DataMusuh> daftarMusuh = new();
    private int turnSekarang = 0;
    private enum StatusBot { MengikutiDinding, MenjagaJarak, Kabur, TembakAman } //status bot untuk strategi greedy
    private StatusBot statusSaatIni = StatusBot.MengikutiDinding;// Default ke dekat dinding saat tidak ada ancaman
    private const double JarakAmanMinimal      = 250.0;  // Jarak minimal dari musuh
    private const double JarakBahaya           = 150.0;  // Jarak berbahaya (langsung kabur)
    private const double RadiusPemindaianBahaya = 300.0; // Radius untuk hitung jumlah musuh dekat
    private const double BatasEnergiKritis     = 30.0;   // HP kritis untuk kabur total
    private const double BatasEnergiTembak     = 40.0;   // HP minimal untuk tembak
    private const double JarakDariDinding      = 60.0;   // Jarak ideal dari dinding saat wall hug
    private const double JarakTembakMaksimal   = 400.0;  // Hanya tembak jika musuh dalam range ini
    private int arahMengitariDinding = 1; // 1 = searah jarum jam, -1 = berlawanan arah jarum jam
    private static double NormalisasiSudut(double sudut)// Normalisasi sudut ke range [-180, 180] untuk kemudahan perhitungan
    {
        while (sudut >  180) sudut -= 360;
        while (sudut < -180) sudut += 360;
        return sudut;
    }
    private bool PosisiDalamArena(double x, double y)// Cek apakah posisi berada dalam batas arena (dengan margin untuk menghindari nabrak dinding)
    {
        return x >= 20 && x <= ArenaWidth - 20 &&
               y >= 20 && y <= ArenaHeight - 20;
    }
    private double HitungSkorBahaya(double posX, double posY)// Hitung skor bahaya untuk posisi tertentu berdasarkan jarak ke musuh dan jarak ke dinding
    {
        double skorBahaya = 0;
        foreach (var musuh in daftarMusuh.Values)// Evaluasi semua musuh yang terdeteksi
        {
            if (turnSekarang - musuh.TurnTerakhirTerdeteksi > 20) // Lewati data lama yang mungkin sudah tidak relevan
                continue;

            double jarakKeMusuh = Math.Sqrt(
                Math.Pow(posX - musuh.PosisiX, 2) + 
                Math.Pow(posY - musuh.PosisiY, 2)
            );
            if (jarakKeMusuh < RadiusPemindaianBahaya)// Semakin dekat dengan musuh, semakin tinggi skor bahaya
            {
                skorBahaya += (RadiusPemindaianBahaya - jarakKeMusuh) / 50.0;
            }
        }
        double jarakKeDindingTerdekat = HitungJarakKeDindingTerdekat(posX, posY); // Semakin dekat dengan dinding, semakin rendah skor bahaya (karena lebih sulit diserang dari belakang)
        if (jarakKeDindingTerdekat > JarakDariDinding * 2)
        {
            skorBahaya += (jarakKeDindingTerdekat - JarakDariDinding) / 30.0;
        }
        return skorBahaya;
    }
    private double HitungJarakKeDindingTerdekat(double posX, double posY)// Hitung jarak ke dinding terdekat dari posisi tertentu
    {
        double jarakKiri   = posX;
        double jarakKanan  = ArenaWidth - posX;
        double jarakBawah  = posY;
        double jarakAtas   = ArenaHeight - posY;

        return Math.Min(Math.Min(jarakKiri, jarakKanan), Math.Min(jarakBawah, jarakAtas));
    }

    private bool SedangDekatDinding()// Cek apakah bot sedang dekat dengan dinding
    {
        return HitungJarakKeDindingTerdekat(X, Y) < JarakDariDinding * 1.5;
    }
    private DataMusuh? CariMusuhTerdekat()// Cari musuh terdekat yang masih relevan (terdeteksi dalam 20 turn terakhir)
    {
        DataMusuh? terdekat = null;
        double jarakTerdekat = double.MaxValue;

        foreach (var musuh in daftarMusuh.Values)
        {
            if (turnSekarang - musuh.TurnTerakhirTerdeteksi > 20) 
                continue;

            double jarak = DistanceTo(musuh.PosisiX, musuh.PosisiY);
            if (jarak < jarakTerdekat)
            {
                jarakTerdekat = jarak;
                terdekat = musuh;
            }
        }

        return terdekat;
    }
    private (double x, double y) CariPojokPalingAman()// Cari pojok arena yang paling aman berdasarkan jarak ke musuh
    {
        double margin = 80.0;
        double[][] pojokArena = {
            new[] { margin, margin },
            new[] { ArenaWidth - margin, margin },
            new[] { margin, ArenaHeight - margin },
            new[] { ArenaWidth - margin, ArenaHeight - margin }
        };

        (double x, double y) pojokTerbaik = (pojokArena[0][0], pojokArena[0][1]);
        double totalJarakTerbaik = -1;
        foreach (var pojok in pojokArena)
        {
            double totalJarak = 0;
            foreach (var musuh in daftarMusuh.Values)
            {
                totalJarak += Math.Sqrt(
                    Math.Pow(pojok[0] - musuh.PosisiX, 2) + 
                    Math.Pow(pojok[1] - musuh.PosisiY, 2)
                );
            }

            if (totalJarak > totalJarakTerbaik)
            {
                totalJarakTerbaik = totalJarak;
                pojokTerbaik = (pojok[0], pojok[1]);
            }
        }

        return pojokTerbaik;
    }
    private bool KondisiAmanUntukTembak(DataMusuh musuh) // Cek apakah kondisi aman untuk menembak musuh (prioritas survival, hanya tembak jika HP cukup dan musuh dalam jarak yang aman)
    {
        if (musuh == null) return false;
        
        double jarak = DistanceTo(musuh.PosisiX, musuh.PosisiY);
        
        // Aman jika: HP cukup, musuh tidak terlalu dekat, dan dalam jangkauan
        return Energy > BatasEnergiTembak && 
               jarak > JarakAmanMinimal && 
               jarak < JarakTembakMaksimal;
    }

    // ─────────────────────────────────────────────────────────────
    public override void Run()// Loop utama bot
    {
        BodyColor   = Color.LimeGreen;
        TurretColor = Color.ForestGreen;
        GunColor    = Color.DarkGreen;
        RadarColor  = Color.Yellow;
        ScanColor   = Color.Yellow;
        BulletColor = Color.OrangeRed;
        AdjustGunForBodyTurn  = false;// Gun tetap mengarah ke target saat bot berbelok
        AdjustRadarForGunTurn = false;// Radar tetap mengarah ke target saat gun berbelok

        while (IsRunning)
        {
            turnSekarang++;
            DataMusuh? musuhTerdekat = CariMusuhTerdekat();
            if (Energy < BatasEnergiKritis)
            {
                statusSaatIni = StatusBot.Kabur;
            }else if (musuhTerdekat != null && DistanceTo(musuhTerdekat.PosisiX, musuhTerdekat.PosisiY) < JarakBahaya)
            {
                statusSaatIni = StatusBot.MenjagaJarak;
            }else if (musuhTerdekat != null && KondisiAmanUntukTembak(musuhTerdekat))
            {
                statusSaatIni = StatusBot.TembakAman;
            }else
            {
                statusSaatIni = StatusBot.MengikutiDinding;
            }

            switch (statusSaatIni)
            {
                case StatusBot.MengikutiDinding:
                    LakukanWallHugging();
                    break;

                case StatusBot.MenjagaJarak:
                    LakukanEvade(musuhTerdekat!);
                    break;

                case StatusBot.TembakAman:
                    TembakDenganAman(musuhTerdekat!);
                    break;

                case StatusBot.Kabur:
                    KaburKePojokAman();
                    break;
            }
            PutarRadarPenuh();
        }
    }

    private void LakukanWallHugging()// Mengikuti dinding untuk meminimalkan serangan dari belakang
    {
        double jarakKiri   = X;
        double jarakKanan  = ArenaWidth - X;
        double jarakBawah  = Y;
        double jarakAtas   = ArenaHeight - Y;
        double jarakMinimal = Math.Min(Math.Min(jarakKiri, jarakKanan),  Math.Min(jarakBawah, jarakAtas));
        double sudutTarget = 0;
        if (jarakMinimal == jarakKiri)
        {            // Dinding kiri berarti jalan ke atas atau bawah
            sudutTarget = arahMengitariDinding > 0 ? 0 : 180;
        }
        else if (jarakMinimal == jarakKanan)
        {             // Dinding kanan berarti jalan ke bawah atau atas
            sudutTarget = arahMengitariDinding > 0 ? 180 : 0;
        }
        else if (jarakMinimal == jarakBawah)
        {              // Dinding bawah berarti jalan ke kanan atau kiri
            sudutTarget = arahMengitariDinding > 0 ? 90 : -90;
        }
        else // jarakAtas
        {
            sudutTarget = arahMengitariDinding > 0 ? -90 : 90;
        }
        double sudutBelok = NormalisasiSudut(sudutTarget - Direction);// Belok ke arah yang sejajar dengan dinding
        TurnRight(sudutBelok);
        if (jarakMinimal < JarakDariDinding * 0.7)// Jika terlalu dekat dengan dinding, mundur sedikit untuk menghindari nabrak
        {
            Back(20);
        }else if (jarakMinimal > JarakDariDinding * 1.5)
        {
            // Belok ke arah dinding terdekat
            double sudutKeDinding = 0;
            if (jarakMinimal == jarakKiri)        sudutKeDinding = -90;
            else if (jarakMinimal == jarakKanan)  sudutKeDinding = 90;
            else if (jarakMinimal == jarakBawah)  sudutKeDinding = 180;
            else                                  sudutKeDinding = 0;
            double belokKeDinding = NormalisasiSudut(sudutKeDinding - Direction);
            TurnRight(belokKeDinding);
            Forward(30);
        }else// Jarak ideal dari dinding, lanjutkan mengitari
        {
            Forward(100);
        }

        Console.WriteLine($" Mengikuti dinding, jarak={jarakMinimal:F0}px, HP={Energy:F0}");
    }
    private void LakukanEvade(DataMusuh musuhTerdekat)
    {
        double jarakKeMusuh = DistanceTo(musuhTerdekat.PosisiX, musuhTerdekat.PosisiY);
        double sudutKeMusuh = BearingTo(musuhTerdekat.PosisiX, musuhTerdekat.PosisiY);
        double sudutMenjauhi = NormalisasiSudut(sudutKeMusuh + 180);
        double[] opsiSudut = { 
            sudutMenjauhi,           // Berlawanan arah langsung
            sudutMenjauhi + 45,      // Diagonal kanan
            sudutMenjauhi - 45,      // Diagonal kiri
        };
        double sudutTerbaik = sudutMenjauhi;
        double skorBahayaTerendah = double.MaxValue;
        foreach (double sudut in opsiSudut)
        {
            double jarakGerak = 100;
            double sudutRad = (Direction + sudut) * Math.PI / 180.0;
            double posXBaru = X + Math.Cos(sudutRad) * jarakGerak;
            double posYBaru = Y + Math.Sin(sudutRad) * jarakGerak;
            if (!PosisiDalamArena(posXBaru, posYBaru)) 
                continue;
            double skorBahaya = HitungSkorBahaya(posXBaru, posYBaru);
            if (skorBahaya < skorBahayaTerendah)
            {
                skorBahayaTerendah = skorBahaya;
                sudutTerbaik = sudut;
            }
        }
        double sudutBelok = NormalisasiSudut(sudutTerbaik - Direction);
        TurnRight(sudutBelok);
        if (jarakKeMusuh < JarakBahaya)
        {
            Back(80);
        }else
        {
            Forward(100);
        }

        Console.WriteLine($"Menghindar dari Bot#{musuhTerdekat.Id}, " + $"jarak={jarakKeMusuh:F0}px, skor_bahaya={skorBahayaTerendah:F2}");
    }

    private void TembakDenganAman(DataMusuh target)
    {
        double jarak = DistanceTo(target.PosisiX, target.PosisiY);
        double sudutGun = NormalisasiSudut(
            BearingTo(target.PosisiX, target.PosisiY) - GunDirection);
        TurnGunRight(sudutGun);
        double daya = 1.0;
        if (Energy > 70 && jarak < 250) 
        {
            daya = 1.5; // Sedikit lebih kuat jika HP sangat aman
        }
        if (GunHeat == 0 && Math.Abs(sudutGun) < 10)
        {
            Fire(daya);
            Console.WriteLine($"[TEMBAK] Bot#{target.Id} jarak={jarak:F0}px, " +
                              $"power={daya}, HP={Energy:F0}");
        }
        if (jarak < JarakAmanMinimal + 50)
        {
            // Mundur sedikit jika terlalu dekat
            double sudutMundur = NormalisasiSudut(
                BearingTo(target.PosisiX, target.PosisiY) - Direction + 180);
            TurnRight(sudutMundur);
            Forward(40);
        }
    }
    private void KaburKePojokAman()
    {
        var (pojokX, pojokY) = CariPojokPalingAman();
        
        double sudutKePojok = NormalisasiSudut(
            BearingTo(pojokX, pojokY) - Direction);
        TurnRight(sudutKePojok);

        double jarakKePojok = DistanceTo(pojokX, pojokY);
        Forward(jarakKePojok);

        Console.WriteLine($"[KABUR] HP KRITIS ({Energy:F0})! Menuju pojok aman " +
                          $"di ({pojokX:F0},{pojokY:F0})");
    }
    private void PutarRadarPenuh()
    {
        TurnRadarRight(360);
    }
    public override void OnScannedBot(ScannedBotEvent e)
    {
        daftarMusuh[e.ScannedBotId] = new DataMusuh
        {
            Id                      = e.ScannedBotId,
            PosisiX                 = e.X,
            PosisiY                 = e.Y,
            Energi                  = e.Energy,
            Kecepatan               = e.Speed,
            Arah                    = e.Direction,
            TurnTerakhirTerdeteksi  = turnSekarang
        };
        Console.WriteLine($" Bot#{e.ScannedBotId} terdeteksi: " + $"energi={e.Energy:F0}, pos=({e.X:F0},{e.Y:F0})");
    }

    public override void OnBotDeath(BotDeathEvent e)
    {
        daftarMusuh.Remove(e.VictimId);
        Console.WriteLine($"[MUSUH MATI] Bot#{e.VictimId} tereliminasi! " + $"Sisa musuh: {daftarMusuh.Count}. " + $"+50 SURVIVAL SCORE!");
    }

    public override void OnHitByBullet(HitByBulletEvent e)
    {
        double arahPeluru = e.Bullet.Direction;
        double sudutHindar = NormalisasiSudut(arahPeluru + 90 - Direction);
        TurnRight(sudutHindar);
        Forward(60);
        Console.WriteLine($"[KENA TEMBAK] HP={Energy:F0}, menghindar tegak lurus!");
    }

    public override void OnHitBot(HitBotEvent e)
    {
        Console.WriteLine($"[TABRAKAN] Mundur untuk hindari damage!");
        Back(50);
        TurnRight(90);
    }

    public override void OnHitWall(HitWallEvent e)
    {
        Console.WriteLine($"[NABRAK DINDING] Balik arah!");
        Back(30);
        TurnRight(90);
        arahMengitariDinding *= -1;
    }
}