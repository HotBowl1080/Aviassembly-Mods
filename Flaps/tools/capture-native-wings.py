"""Capture native Wing prefabs and baked child meshes without changing game files.

Install UnityPy and numpy, or pass --dependencies to an existing installation.
This captures stock geometry; use GeometryRunner for the generated flap poses.
"""
import sys,pathlib,json,re,argparse
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--game-dir',required=True,type=pathlib.Path)
parser.add_argument('--dependencies',type=pathlib.Path)
parser.add_argument('--output',required=True,type=pathlib.Path)
parser.add_argument('--straight-output',required=True,type=pathlib.Path)
args=parser.parse_args()
if args.dependencies:sys.path.insert(0,str(args.dependencies))
import UnityPy,numpy as np
from UnityPy.helpers.MeshHelper import MeshHandler
root=args.game_dir/'Aviassembly_Data'
env=UnityPy.load(*[str(p) for p in root.glob('*.assets')])
def matrix(t):
    q=t.m_LocalRotation;x,y,z,w=q.x,q.y,q.z,q.w
    r=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])
    m=np.eye(4);s=t.m_LocalScale;m[:3,:3]=r@np.diag([s.x,s.y,s.z]);p=t.m_LocalPosition;m[:3,3]=[p.x,p.y,p.z];return m
wings=[];straight=None;seen=set()
for o in env.objects:
    if o.type.name!='GameObject':continue
    g=o.read();classes=[]
    for c in g.m_Component:
        p=c.component
        if p.type.name=='MonoBehaviour':
            d=p.deref().read_typetree(check_read=False);s=d.get('m_Script')
            if s:
                try: classes.append(p.deref().read(check_read=False).m_Script.read().m_ClassName)
                except Exception: pass
    if 'Wing' not in classes:continue
    if g.m_Name in seen:raise ValueError('Duplicate native Wing prefab: '+g.m_Name)
    seen.add(g.m_Name)
    print('WING',g.m_Name,classes,flush=True)
    tr=next(c.component.read() for c in g.m_Component if c.component.type.name=='Transform')
    shapes=[]
    def walk(t,m,role='wing'):
        global straight
        go=t.m_GameObject.read();role='aileron' if 'aileron' in go.m_Name.lower() else role
        for c in go.m_Component:
            if c.component.type.name!='MeshFilter':continue
            mesh=c.component.read().m_Mesh.read();h=MeshHandler(mesh);h.process()
            p=np.array(h.m_Vertices);ns=np.array(h.m_Normals);tri=h.get_triangles()
            if not len(p):continue
            p=p@m[:3,:3].T+m[:3,3]
            if ns.shape!=p.shape:ns=np.zeros_like(p)
            else: ns=ns@np.linalg.inv(m[:3,:3]);lens=np.linalg.norm(ns,axis=1);ns=ns/np.maximum(lens[:,None],1e-20)
            tris=[np.array(s).reshape(-1,3) for s in tri]
            if np.linalg.det(m[:3,:3])<0:tris=[s[:,[0,2,1]] for s in tris]
            shape=dict(name=go.m_Name,role=role,positions=p.ravel().tolist(),normals=ns.ravel().tolist(),submeshes=[dict(triangles=s.ravel().tolist()) for s in tris])
            shapes.append(shape)
            if g.m_Name=='StraightWing' and go.m_Name=='StraightWing':
                straight=dict(positions=p.tolist(),normals=ns.tolist(),uv=h.m_UV0,triangles=[s.ravel().tolist() for s in tris]);print('STRAIGHT',len(p),'bounds',p.min(axis=0),p.max(axis=0),'subs',[len(s) for s in tris],flush=True)
        for c in t.m_Children:walk(c.read(),m@matrix(c.read()),role)
    walk(tr,np.eye(4))
    name=re.sub(r'(?<=[a-z])(?=[A-Z0-9])',' ',g.m_Name)
    slug=re.sub(r'[^a-z0-9]+','-',name.lower()).strip('-')
    if g.m_Name=='BigWing':continue
    wings.append(dict(id=slug,name=name,format='raw',version='stock',hasFlaps=False,meshes=shapes))
if straight is None:raise ValueError('StraightWing native mesh was not found')
args.output.write_text(json.dumps(dict(schemaVersion=1,wings=wings)),encoding='utf-8')
args.straight_output.write_text(json.dumps(straight),encoding='utf-8')
print('Captured',len(seen),'native Wing prefabs; Big Wing preview remains on its existing v0.20 adapter.')

