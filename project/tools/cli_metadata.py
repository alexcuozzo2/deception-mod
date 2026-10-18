"""Read-only subset of ECMA-335 metadata for supplied generated wrappers (not executor)."""
from pathlib import Path
import struct,json,sys
CODES={
'TypeDefOrRef':(2,[2,1,27]),'HasConstant':(2,[4,8,23]),'HasCustomAttribute':(5,[6,4,1,2,8,9,10,0,14,23,20,17,26,27,32,35,38,39,40,42,44,43]),
'HasFieldMarshal':(1,[4,8]),'HasDeclSecurity':(2,[2,6,32]),'MemberRefParent':(3,[2,1,26,6,27]),'HasSemantics':(1,[20,23]),
'MethodDefOrRef':(1,[6,10]),'MemberForwarded':(1,[4,6]),'Implementation':(2,[38,35,39]),'CustomAttributeType':(3,[None,None,6,10,None]),
'ResolutionScope':(2,[0,26,35,1]),'TypeOrMethodDef':(1,[2,6])}
S={0:['u2','s','g','g','g'],1:['ResolutionScope','s','s'],2:['u4','s','s','TypeDefOrRef',4,6],3:[4],4:['u2','s','b'],5:[6],6:['u4','u2','u2','s','b',8],7:[8],8:['u2','u2','s'],9:[2,'TypeDefOrRef'],10:['MemberRefParent','s','b'],11:['u2','HasConstant','b'],12:['HasCustomAttribute','CustomAttributeType','b'],13:['HasFieldMarshal','b'],14:['u2','HasDeclSecurity','b'],15:['u2','u4',2],16:['u4',4],17:['b'],18:[2,20],19:[20],20:['u2','s','TypeDefOrRef'],21:[2,23],22:[23],23:['u2','s','b'],24:['u2',6,'HasSemantics'],25:[2,'MethodDefOrRef','MethodDefOrRef'],26:['s'],27:['b'],28:['u2','MemberForwarded','s',26],29:['u4',4],30:['u4','u4'],31:['u4'],32:['u4','u2','u2','u2','u2','u4','b','s','s'],33:['u4'],34:['u4','u4','u4'],35:['u2','u2','u2','u2','u4','b','s','s','b'],36:['u4',35],37:['u4','u4','u4',35],38:['u4','s','b'],39:['u4','u4','s','s','Implementation'],40:['u4','u4','s','Implementation'],41:[2,2],42:['u2','u2','TypeOrMethodDef','s'],43:['MethodDefOrRef','b'],44:[42,'TypeDefOrRef']}
def comp(b,p=0):
 x=b[p]
 if x<128:return x,p+1
 if x<192:return ((x&63)<<8)|b[p+1],p+2
 return ((x&31)<<24)|int.from_bytes(b[p+1:p+4],'big'),p+4
class CLI:
 def __init__(self,path):
  self.path=Path(path);self.b=self.path.read_bytes();b=self.b
  self.u16=lambda p:struct.unpack_from('<H',b,p)[0];self.u32=lambda p:struct.unpack_from('<I',b,p)[0]
  pe=self.u32(0x3c);opt=pe+24;ns=self.u16(pe+6);osz=self.u16(pe+20)
  self.sections=[struct.unpack_from('<8sIIIIIIHHI',b,opt+osz+i*40) for i in range(ns)]
  dd=opt+(112 if self.u16(opt)==0x20b else 96);cli=self.off(self.u32(dd+14*8));meta=self.off(self.u32(cli+8))
  p=meta+16+self.u32(meta+12);p=(p+3)&~3;cnt=self.u16(p+2);p+=4;self.streams={}
  for i in range(cnt):
   o,n=struct.unpack_from('<II',b,p);p+=8;q=b.index(0,p);name=b[p:q].decode();p=(q+4)&~3;self.streams[name]=(meta+o,n)
  self.strings=self.streams['#Strings'][0];self.blobs=self.streams['#Blob'][0];self.us=self.streams.get('#US',(0,0))[0]
  p=self.streams.get('#~',self.streams.get('#-'))[0];self.heap=b[p+6];valid=struct.unpack_from('<Q',b,p+8)[0];p+=24
  self.count={}
  for i in range(64):
   if valid>>i&1:self.count[i]=self.u32(p);p+=4
  self.rows={};self.offsets={}
  for i,n in self.count.items():
   self.offsets[i]=p;cols=S[i];rs=sum(self.size(x) for x in cols);arr=[]
   for _ in range(n):
    row=[]
    for x in cols:
     z=self.size(x);row.append(int.from_bytes(b[p:p+z],'little'));p+=z
    arr.append(row)
   self.rows[i]=arr
  self.names={};self.owners={};self.fieldowners={}
  for i,r in enumerate(self.rows.get(2,[]),1):
   self.names[i]=(self.s(r[2])+'.' if self.s(r[2]) else '')+self.s(r[1])
  for nested,enclosing in self.rows.get(41,[]):self.names[nested]=self.names[enclosing]+'+'+self.names[nested]
  td=self.rows.get(2,[])
  for i,r in enumerate(td,1):
   end=td[i][5] if i<len(td) else self.count.get(6,0)+1
   for m in range(r[5],end):self.owners[m]=i
   end=td[i][4] if i<len(td) else self.count.get(4,0)+1
   for f in range(r[4],end):self.fieldowners[f]=i
 def off(self,rva):
  for s in self.sections:
   if s[2]<=rva<s[2]+max(s[1],s[3]):return s[4]+rva-s[2]
  raise ValueError(hex(rva))
 def size(self,t):
  if t=='u2':return 2
  if t=='u4':return 4
  if t in ['s','g','b']:return 4 if self.heap & {'s':1,'g':2,'b':4}[t] else 2
  if isinstance(t,int):return 4 if self.count.get(t,0)>=65536 else 2
  bits,tbls=CODES[t];return 4 if max(self.count.get(x,0) for x in tbls)>=1<<(16-bits) else 2
 def row(self,t,i):return self.rows[t][i-1]
 def s(self,o):
  p=self.strings+o;q=self.b.index(0,p);return self.b[p:q].decode('utf8')
 def blob(self,o):
  n,p=comp(self.b,self.blobs+o);return self.b[p:p+n]
 def typeref(self,tok):
  tag=tok&3;i=tok>>2
  if tag==0:return self.names.get(i,'td'+str(i))
  if tag==1:
   r=self.row(1,i)
   if (r[0]&3)==3:return self.typeref((r[0]>>2)*4+1)+'+'+self.s(r[1])
   return (self.s(r[2])+'.' if self.s(r[2]) else '')+self.s(r[1])
  if tag==2:return self.type(self.blob(self.row(27,i)[0]))[0]
  return 'badtype'
 def type(self,b,p=0):
  el=b[p];p+=1
  simple={1:'System.Void',2:'System.Boolean',3:'System.Char',4:'System.SByte',5:'System.Byte',6:'System.Int16',7:'System.UInt16',8:'System.Int32',9:'System.UInt32',10:'System.Int64',11:'System.UInt64',12:'System.Single',13:'System.Double',14:'System.String',24:'System.IntPtr',25:'System.UIntPtr',28:'System.Object',22:'System.TypedReference'}
  if el in simple:return simple[el],p
  if el in (0x11,0x12):n,p=comp(b,p);return self.typeref(n),p
  if el in (0x0f,0x10,0x1d,0x45):n,p=self.type(b,p);return n+{0xf:'*',0x10:'&',0x1d:'[]',0x45:' pinned'}[el],p
  if el in (0x13,0x1e):n,p=comp(b,p);return ('!' if el==0x13 else '!!')+str(n),p
  if el in (0x1f,0x20):n,p=comp(b,p);s,p=self.type(b,p);return s,p
  if el==0x15:
   n,p=self.type(b,p);count,p=comp(b,p);args=[]
   for i in range(count):a,p=self.type(b,p);args.append(a)
   return n+'<'+','.join(args)+'>',p
  if el==0x14:
   n,p=self.type(b,p);rank,p=comp(b,p);ns,p=comp(b,p)
   for _ in range(ns):v,p=comp(b,p)
   nb,p=comp(b,p)
   for _ in range(nb):v,p=comp(b,p)
   return n+'['+','*(rank-1)+']',p
  if el==0x1b:return 'fnptr',len(b)
  return 'EL'+hex(el),p
 def sig(self,b):
  p=1
  if b[0]&0x10:g,p=comp(b,p)
  n,p=comp(b,p);ret,p=self.type(b,p);args=[]
  for _ in range(n):s,p=self.type(b,p);args.append(s)
  return ret,args
 def methods(self,typepattern):
  ans=[]
  for i,r in enumerate(self.rows[6],1):
   owner=self.names[self.owners[i]]
   if typepattern==owner or (typepattern.endswith('*') and owner.startswith(typepattern[:-1])):
    ret,args=self.sig(self.blob(r[4]));end=self.rows[6][i][5] if i<len(self.rows[6]) else self.count.get(8,0)+1
    pars={v[1]:self.s(v[2]) for v in self.rows.get(8,[])[r[5]-1:end-1]}
    ans.append(dict(owner=owner,rid=i,name=self.s(r[3]),ret=ret,args=args,params=pars,flags=r[2],rva=r[0]))
  return ans
 def token(self,t):
  tab=t>>24;rid=t&0xffffff
  if tab==0x70:
   n,p=comp(self.b,self.us+rid);return repr(self.b[p:p+n-1].decode('utf-16-le',errors='replace'))
  if tab==6:
   r=self.row(6,rid);return self.names[self.owners[rid]]+'::'+self.s(r[3])
  if tab==10:
   r=self.row(10,rid);bits,tbl=CODES['MemberRefParent'];tag=r[0]&7;nr=r[0]>>3;tb=tbl[tag]
   owner=self.names[nr] if tb==2 else (self.typeref(nr*4+1) if tb==1 else (self.typeref(nr*4+2) if tb==27 else str(r[0])))
   return owner+'::'+self.s(r[1])
  if tab==4:return self.names[self.fieldowners[rid]]+'::'+self.s(self.row(4,rid)[1])
  if tab==1:return self.typeref(rid*4+1)
  if tab==2:return self.names[rid]
  return hex(t)
 def il(self,rid):
  r=self.row(6,rid);p=self.off(r[0]);x=self.b[p]
  if x&3==2:n=x>>2;p+=1
  else:n=self.u32(p+4);p+=(self.u16(p)>>12)*4
  return self.b[p:p+n]
