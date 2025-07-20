import sys

def examine_offset(filename, offset):
    with open(filename, 'rb') as f:
        data = f.read()
    
    start = max(0, offset - 20)
    end = min(len(data), offset + 20)
    
    print(f"Data around offset {offset} in {filename}:")
    print(f"Context: bytes {start} to {end}")
    
    for i in range(start, end):
        char = chr(data[i]) if 32 <= data[i] <= 126 else f'\\x{data[i]:02x}'
        marker = ' <<< ' if i == offset else ''
        print(f"  {i:3d}: {data[i]:3d} ({char}){marker}")

# Check both files at offset 187
examine_offset(r'c:\Users\DELL\Downloads\big-buck-bunny.torrent', 187)
print("\n" + "="*50 + "\n")
examine_offset(r'c:\Users\DELL\Downloads\cosmos-laundromat.torrent', 187)
