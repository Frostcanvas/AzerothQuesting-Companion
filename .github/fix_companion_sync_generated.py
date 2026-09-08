from pathlib import Path

path = Path("src/AzerothQuesting.Companion/MainForm.cs")
text = path.read_text(encoding="utf-8")
bad = '$"Pending Observations were kept locally.\n\n{ex.Message}"'
good = '$"Pending Observations were kept locally.\\n\\n{ex.Message}"'
if bad not in text:
    raise SystemExit("Expected generated multiline message was not found")
path.write_text(text.replace(bad, good, 1), encoding="utf-8")
