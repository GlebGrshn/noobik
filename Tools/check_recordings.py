"""Validate silent 1080p promo clips, make contact sheets and join a preview reel."""
import argparse
import json
from pathlib import Path
import subprocess


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('folder', type=Path)
    parser.add_argument('--ffmpeg', required=True, type=Path)
    args = parser.parse_args()
    probe = args.ffmpeg.with_name('ffprobe.exe' if args.ffmpeg.suffix == '.exe' else 'ffprobe')
    expected = {'01_yard': 12, '02_descent': 16, '03_roots': 8, '04_slate': 8,
                '05_crystals': 8, '06_magma_meteor': 16, '07_cthulhu': 26, '08_victory': 10}
    records = []
    for name, seconds in expected.items():
        path = args.folder / (name + '.mp4')
        result = subprocess.run([str(probe), '-v', 'error', '-show_streams', '-show_format', '-of', 'json', str(path)], check=True, capture_output=True, text=True)
        data = json.loads(result.stdout)
        streams = data['streams']
        assert len(streams) == 1 and streams[0]['codec_type'] == 'video', (name, 'unexpected audio or missing video')
        video = streams[0]
        assert (video['width'], video['height']) == (1920, 1080), name
        assert video['codec_name'] == 'h264' and video['pix_fmt'] == 'yuv420p', name
        assert video['avg_frame_rate'] == '30/1' and int(video['nb_frames']) == seconds * 30, name
        assert abs(float(data['format']['duration']) - seconds) < .05, name
        decoded = subprocess.run([str(args.ffmpeg), '-v', 'error', '-i', str(path), '-f', 'null', '-'], check=True, capture_output=True, text=True)
        assert not decoded.stderr.strip(), (name, decoded.stderr)
        # Three evenly spaced decoded frames; these also catch flipped/corrupt/blank output visually.
        subprocess.run([str(args.ffmpeg), '-v', 'error', '-y', '-i', str(path), '-vf', f'fps=3/{seconds},scale=640:360,tile=3x1', '-frames:v', '1', '-update', '1', str(args.folder / (name + '_contact.png'))], check=True)
        records.append({'file': path.name, 'seconds': seconds, 'frames': seconds * 30, 'bytes': path.stat().st_size, 'decoded': True})
        print(name, 'OK', seconds, 'seconds', flush=True)
    concat = args.folder / 'reel.txt'
    concat.write_text(''.join("file '" + name + ".mp4'\n" for name in expected), encoding='utf-8')
    subprocess.run([str(args.ffmpeg), '-v', 'error', '-y', '-f', 'concat', '-safe', '0', '-i', str(concat), '-c', 'copy', '-movflags', '+faststart', str(args.folder / 'Nubik_Promo_FullHD.mp4')], check=True)
    (args.folder / 'validation.json').write_text(json.dumps({'resolution': '1920x1080', 'fps': 30, 'audio': False, 'total_seconds': sum(expected.values()), 'clips': records}, indent=2), encoding='utf-8')


if __name__ == '__main__':
    main()
