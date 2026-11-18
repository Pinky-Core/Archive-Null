"""Generate distinct provisional voices. The game itself only uses the exported WAV files."""
import argparse
import asyncio
import json
import subprocess
from pathlib import Path
import edge_tts

async def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--ffmpeg', required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    directory = root / 'Assets/ArchiveNull/Resources/CaseAudio'
    temporary = root / '.utmp/CaseVoices'
    temporary.mkdir(parents=True, exist_ok=True)
    voices = json.loads((directory / 'Voices.json').read_text(encoding='utf-8-sig'))
    for name, voice in voices.items():
        source = temporary / f'{name}.mp3'
        await edge_tts.Communicate(voice['text'], voice['voice'], rate=voice['rate'], pitch=voice['pitch']).save(str(source))
        filters = 'highpass=f=120,lowpass=f=7600,loudnorm=I=-18:TP=-2:LRA=9'
        subprocess.run([args.ffmpeg, '-hide_banner', '-loglevel', 'error', '-y', '-i', str(source), '-af', filters, '-ar', '24000', '-ac', '1', str(directory / f'{name}.wav')], check=True)
        print(f"{name}: {voice['voice']} ({voice['rate']}, {voice['pitch']})", flush=True)

if __name__ == '__main__':
    asyncio.run(main())
