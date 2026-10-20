from pathlib import Path
import subprocess, json, shutil, hashlib, os, stat

repo=Path('/opt/future-viewer')
stage=Path('/tmp/fv-birthday-d94a73c6')
release=Path('/opt/fv-app/releases/birthday-d94a73c6')
expected='24d7a3238ee614d0a923adfd100e5790b95cf611'
sha='d94a73c6f59b8baadd0818e7d4db0cf888166d18'

def run(args, **kwargs):
    return subprocess.run(args,check=True,cwd=repo,**kwargs)
def output(args):
    return subprocess.check_output(args,cwd=repo,text=True).strip()

assert os.geteuid()==0
assert all((stage/name).is_file() for name in ['certificate.jpg','postcard.jpg','production-settings.json','images.tar.gz','images.sha256']), 'Incomplete staging directory'
env_values=dict(line.split('=',1) for line in Path('/opt/fv-app/.env.production').read_text().splitlines() if '=' in line and not line.lstrip().startswith('#'))
assert env_values.get('ReverseProxy__KnownProxies__0','').strip("\"'")=='172.18.0.5', 'Existing trusted proxy differs'
assert not Path('/opt/fv-app/birthday').exists(), 'Private assets already exist'
assert not any(k.startswith('Birthday__') for k in env_values), 'Birthday configuration already exists'
assert output(['git','-c','safe.directory=/opt/future-viewer','rev-parse','HEAD'])==expected, 'Production revision changed'
run(['git','-c','safe.directory=/opt/future-viewer','diff','--quiet'])
containers=json.loads(output(['docker','inspect','future-viewer-backend-1','future-viewer-frontend-1','future-viewer-caddy-1','future-viewer-postgres-1']))
assert containers[2]['NetworkSettings']['Networks']['future-viewer_default']['IPAddress']=='172.18.0.5'
assert containers[0]['Config']['Image']=='future-viewer-backend:87cae8e9ded3bdb2080180e0e3f1b6d546d50ec4'
assert containers[1]['Config']['Image']=='future-viewer-frontend:'+expected
assert not release.exists(), 'Release snapshot already exists; inspect before retrying'
release.mkdir(parents=True,mode=0o700);release.chmod(0o700)
envfile=Path('/opt/fv-app/.env.production')
shutil.copy2(envfile,release/'env.production.before');(release/'env.production.before').chmod(0o600)
shutil.copy2('/opt/fv-app/legacy-edge.Caddyfile',release/'Caddyfile.before')
before={c['Name']:{'id':c['Id'],'image':c['Config']['Image']} for c in containers}
(release/'before.json').write_text(json.dumps({'revision':expected,'containers':before},indent=2))
checksum=(stage/'images.sha256').read_text().split()[0]
digest=hashlib.sha256()
with (stage/'images.tar.gz').open('rb') as archive_file:
    for chunk in iter(lambda:archive_file.read(1024*1024),b''): digest.update(chunk)
assert digest.hexdigest()==checksum
run(['docker','load','-i',str(stage/'images.tar.gz')])
for service in ['backend','frontend']:
    assert output(['docker','image','inspect','future-viewer-'+service+':'+sha,'--format','{{.Architecture}}'])=='amd64'
# Fetch as the repository owner, using its existing deploy key.
run(['sudo','-u','deploy','git','fetch','origin','main'])
run(['git','-c','safe.directory=/opt/future-viewer','merge-base','--is-ancestor',sha,'origin/main'])
assert not output(['git','-c','safe.directory=/opt/future-viewer','diff','--name-only',sha,'origin/main','--','backend','frontend','config','docker-compose.prod.yml','docker-compose.prod.prebuilt.yml','docker-compose.prod.legacy-edge.yml','docker-compose.prod.birthday.yml']), 'Application inputs changed since QA'
assert not output(['git','-c','safe.directory=/opt/future-viewer','diff','--name-only',expected,sha,'--','backend/src/FutureViewer.Infrastructure/Persistence/Migrations']), 'Unexpected schema changes'
run(['sudo','-u','deploy','git','merge','--ff-only',sha])
assets=Path('/opt/fv-app/birthday')
assert not assets.exists(), 'Private assets already exist; inspect before replacing'
assets.mkdir(mode=0o750);os.chown(assets,1654,1654)
for name in ['certificate.jpg','postcard.jpg']:
    dest=assets/name;shutil.copyfile(stage/name,dest);os.chown(dest,1654,1654);dest.chmod(0o640)
settings=json.loads((stage/'production-settings.json').read_text())
assert set(settings)=={'Birthday__KeyHash','Birthday__CertificatePath','Birthday__PostcardPath'}
assert len(settings['Birthday__KeyHash'])==64
text=envfile.read_text(); lines=text.splitlines()
assert not any(line.split('=',1)[0].strip() in settings for line in lines), 'Birthday config already set'
newfile=release/'env.production.new'
newfile.write_text(text.rstrip()+'\n\n# Private birthday invitation\n'+'\n'.join(k+'='+v for k,v in settings.items())+'\n');newfile.chmod(0o600)
oldstat=envfile.stat();os.chown(newfile,oldstat.st_uid,oldstat.st_gid)
os.replace(newfile,envfile)
env=dict(os.environ,FV_IMAGE_TAG=sha,FV_SHARED_EDGE_PORT='18080',FV_LEGACY_EDGE_IP='172.18.0.5')
compose=['docker','compose','--env-file',str(envfile),'-f','docker-compose.prod.yml','-f','docker-compose.prod.prebuilt.yml','-f','docker-compose.prod.legacy-edge.yml','-f','docker-compose.prod.birthday.yml','-p','future-viewer']
run(compose+['config','--quiet'],env=env)
run(compose+['up','-d','--no-deps','--no-build','--pull','never','backend','frontend'],env=env)
for name in ['future-viewer-caddy-1','future-viewer-postgres-1']:
    assert output(['docker','inspect',name,'--format','{{.Id}}'])==before['/'+name]['id'], 'Unrelated container changed'
assert Path('/opt/fv-app/legacy-edge.Caddyfile').read_bytes()==(release/'Caddyfile.before').read_bytes()
(release/'deployed-sha').write_text(sha+'\n')
print('Deployed '+sha+'; preserved proxy, database and TLS configuration. Snapshot: '+str(release))

run(['docker','compose','--env-file',str(envfile),'-f','docker-compose.prod.yml','-p','future-viewer','ps'],env=env)
