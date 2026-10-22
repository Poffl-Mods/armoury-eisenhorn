"""Read-only UnityFS shader-name audit using macOS raw LZ4 decoding.
Usage: python3 inspect_native_shader_bundle.py /path/to/Bundles/shaders
The output is build evidence, not an installation or game modification.
"""
import ctypes,struct,sys,json,hashlib
from pathlib import Path
p=Path(sys.argv[1]).resolve()
assert p.stat().st_size < 64*1024*1024
s=p.read_bytes();o=0
lib=ctypes.CDLL('/usr/lib/libcompression.dylib');lib.compression_decode_buffer.restype=ctypes.c_size_t;lib.compression_decode_buffer.argtypes=[ctypes.c_void_p,ctypes.c_size_t,ctypes.c_void_p,ctypes.c_size_t,ctypes.c_void_p,ctypes.c_int]
def dec(b,n,c):
 if c==0: assert len(b)==n;return b
 assert c in (2,3)
 out=ctypes.create_string_buffer(n);src=ctypes.create_string_buffer(b)
 got=lib.compression_decode_buffer(out,n,src,len(b),None,0x101)
 assert got==n,(got,n,c)
 return out.raw

def string():
 global o
 e=s.index(b'\0',o);v=s[o:e].decode();o=e+1;return v
assert string()=='UnityFS'
v=struct.unpack_from('>I',s,o)[0];o+=4
bundle_unity=string(); engine_unity=string()
length,pk,un,flags=struct.unpack_from('>QIII',s,o);o+=20
if v>=7:o=(o+15)&~15
io=length-pk if flags&0x80 else o
info=dec(s[io:io+pk],un,flags&63)
if not flags&0x80:o+=pk
if flags&0x200:o=(o+15)&~15
count=struct.unpack_from('>I',info,16)[0]
assert count<100000
i=20;allout=[]
for _ in range(count):
 n,csize,c=struct.unpack_from('>IIH',info,i);i+=10
 assert sum(map(len,allout))+n <= 128*1024*1024
 allout.append(dec(s[o:o+csize],n,c&63));o+=csize
raw=b''.join(allout)
names=['Owlcat/Unlit','Owlcat/Lit']
assert all(name.encode() in raw for name in names)
print(json.dumps({'bundlePath':str(p),'sha256':hashlib.sha256(s).hexdigest(),'unityVersion':engine_unity,'serializedShaderNamesFound':names,'decompressedBytes':len(raw)},indent=2))
