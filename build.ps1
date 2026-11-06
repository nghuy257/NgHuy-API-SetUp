param(
    [string]$IconSource = "$PSScriptRoot\assets\nghuy-source.jpg"
)

$ErrorActionPreference = 'Stop'
$project = $PSScriptRoot
$assets = Join-Path $project 'assets'
$dist = Join-Path $project 'dist'
$sourceCopy = Join-Path $assets 'nghuy-source.jpg'
$logoPng = Join-Path $assets 'nghuy.png'
$iconFile = Join-Path $assets 'nghuy.ico'
$output = Join-Path $dist 'NgHuyAPI.exe'
$compiler = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path -LiteralPath $IconSource -PathType Leaf)) {
    if (Test-Path -LiteralPath $sourceCopy -PathType Leaf) {
        $IconSource = $sourceCopy
    } else {
        throw "Icon source not found: $IconSource"
    }
}

New-Item -ItemType Directory -Path $assets -Force | Out-Null
New-Item -ItemType Directory -Path $dist -Force | Out-Null
if ((Resolve-Path -LiteralPath $IconSource).Path -ne (Resolve-Path -LiteralPath $sourceCopy -ErrorAction SilentlyContinue).Path) {
    Copy-Item -LiteralPath $IconSource -Destination $sourceCopy -Force
}

$assetBuilder = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class NgHuyAssetBuilder
{
    public static void Build(string sourcePath, string pngPath, string icoPath)
    {
        using (Bitmap source = new Bitmap(sourcePath))
        using (Bitmap logo = CropNgHuy(source, 512))
        {
            logo.Save(pngPath, ImageFormat.Png);
            WriteIcon(logo, icoPath, new int[] { 16, 24, 32, 48, 64, 128, 256 });
        }
    }

    private static Bitmap CropNgHuy(Bitmap source, int outputSize)
    {
        // Isolate the N symbol from the supplied square brand sheet, excluding the wordmark.
        int x = (int)Math.Round(source.Width * 0.263);
        int y = (int)Math.Round(source.Height * 0.066);
        int width = Math.Max(1, (int)Math.Round(source.Width * 0.477));
        int height = Math.Max(1, (int)Math.Round(source.Height * 0.375));
        width = Math.Min(width, source.Width - x);
        height = Math.Min(height, source.Height - y);
        int padding = outputSize / 24;
        double scale = (double)(outputSize - 2 * padding) / Math.Max(width, height);
        int drawWidth = Math.Max(1, (int)Math.Round(width * scale));
        int drawHeight = Math.Max(1, (int)Math.Round(height * scale));

        Bitmap output = new Bitmap(outputSize, outputSize, PixelFormat.Format32bppArgb);
        output.SetResolution(96, 96);
        using (Graphics graphics = Graphics.FromImage(output))
        {
            graphics.Clear(Color.Transparent);
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.DrawImage(
                source,
                new Rectangle((outputSize - drawWidth) / 2, (outputSize - drawHeight) / 2, drawWidth, drawHeight),
                new Rectangle(x, y, width, height),
                GraphicsUnit.Pixel);
        }

        RemoveLightBackground(output);
        return output;
    }

    private static void RemoveLightBackground(Bitmap image)
    {
        Rectangle bounds = new Rectangle(0, 0, image.Width, image.Height);
        BitmapData data = image.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            int bytes = Math.Abs(data.Stride) * data.Height;
            byte[] pixels = new byte[bytes];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, bytes);

            for (int y = 0; y < data.Height; y++)
            {
                int row = y * data.Stride;
                for (int x = 0; x < data.Width; x++)
                {
                    int index = row + (x * 4);
                    int b = pixels[index];
                    int g = pixels[index + 1];
                    int r = pixels[index + 2];
                    int min = Math.Min(r, Math.Min(g, b));
                    int max = Math.Max(r, Math.Max(g, b));
                    int spread = max - min;

                    if (min >= 224 && spread <= 31)
                    {
                        pixels[index + 3] = 0;
                    }
                    else if (min > 202 && spread <= 34)
                    {
                        int alpha = (224 - min) * 12;
                        pixels[index + 3] = (byte)Math.Max(0, Math.Min(255, alpha));
                    }
                }
            }

            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, data.Scan0, bytes);
        }
        finally
        {
            image.UnlockBits(data);
        }
    }

    private static void WriteIcon(Bitmap source, string path, int[] sizes)
    {
        List<byte[]> frames = new List<byte[]>();
        foreach (int size in sizes)
        {
            using (Bitmap frame = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            using (Graphics graphics = Graphics.FromImage(frame))
            {
                graphics.Clear(Color.Transparent);
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.DrawImage(source, new Rectangle(0, 0, size, size));
                using (MemoryStream png = new MemoryStream())
                {
                    frame.Save(png, ImageFormat.Png);
                    frames.Add(png.ToArray());
                }
            }
        }

        using (FileStream stream = File.Create(path))
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)sizes.Length);
            int offset = 6 + (16 * sizes.Length);
            for (int i = 0; i < sizes.Length; i++)
            {
                writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write((uint)frames[i].Length);
                writer.Write((uint)offset);
                offset += frames[i].Length;
            }
            foreach (byte[] frame in frames)
            {
                writer.Write(frame);
            }
        }
    }
}
'@

Add-Type -TypeDefinition $assetBuilder -ReferencedAssemblies 'System.Drawing'
[NgHuyAssetBuilder]::Build($sourceCopy, $logoPng, $iconFile)

if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    throw "C# compiler not found: $compiler"
}

$sources = @(
    (Join-Path $project 'Program.cs'),
    (Join-Path $project 'Core.cs'),
    (Join-Path $project 'Integration.cs'),
    (Join-Path $project 'MainForm.cs')
)

$arguments = @(
    '/nologo',
    '/target:winexe',
    '/optimize+',
    '/platform:anycpu',
    "/out:$output",
    "/win32icon:$iconFile",
    "/win32manifest:$(Join-Path $project 'app.manifest')",
    "/resource:$logoPng,NgHuyAPI.NgHuy.png",
    '/reference:System.dll',
    '/reference:System.Core.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll',
    '/reference:System.Net.Http.dll',
    '/reference:System.IO.Compression.dll',
    '/reference:System.IO.Compression.FileSystem.dll',
    '/reference:System.Security.dll',
    '/reference:System.Web.Extensions.dll'
) + $sources

& $compiler @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE"
}

$hash = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash
Write-Host "Built: $output"
Write-Host "SHA256: $hash"
