# SPDX-License-Identifier: GPL-3.0-or-later
# Local-only development fixture; requires explicitly supplied trusted extension/runtime files.
import argparse,hashlib,http.server,io,json,math,pathlib,subprocess,threading,urllib.parse,wave,struct
p=argparse.ArgumentParser();p.add_argument('--java',required=True);p.add_argument('--classpath',required=True);p.add_argument('--extension',required=True);p.add_argument('--converted',required=True);p.add_argument('--entry',required=True);p.add_argument('--output',required=True);p.add_argument('--dotnet',required=True);p.add_argument('--project',required=True);a=p.parse_args()
stream=io.BytesIO()
with wave.open(stream,'wb') as w:
 w.setnchannels(1);w.setsampwidth(2);w.setframerate(16000);w.writeframes(b''.join(struct.pack('<h',int(math.sin(i*2*math.pi*440/16000)*1000)) for i in range(16000*20)))
audio=stream.getvalue();counts={'search':0,'stream':0}
class Fixture(http.server.BaseHTTPRequestHandler):
 def log_message(self,*args):pass
 def do_GET(self):
  u=urllib.parse.urlsplit(self.path);q=urllib.parse.parse_qs(u.query);get=lambda k:q.get(k,[''])[0]
  if get('u')!='fixture' or get('t')!=hashlib.md5(('probe-password'+get('s')).encode()).hexdigest():self.send_error(403);return
  if u.path.endswith('/search3'):
   counts['search']+=1
   body=json.dumps({'subsonic-response':{'status':'ok','searchResult3':{'song':[{'id':'fixture-track','isDir':False,'title':'Synthetic probe tone','duration':20,'bitRate':256,'contentType':'audio/wav'}]}}}).encode();self.send_response(200);self.send_header('Content-Type','application/json');self.send_header('Content-Length',str(len(body)));self.end_headers();self.wfile.write(body)
  elif u.path.endswith('/stream'):
   if get('format')!='raw' or get('maxBitRate')!='0':self.send_error(400);return
   counts['stream']+=1;offset=0
   try:offset=int(self.headers.get('Range','bytes=0-').split('=')[1].split('-')[0])
   except ValueError:self.send_error(400);return
   if not 0<=offset<len(audio):self.send_error(416);return
   self.send_response(206 if offset else 200);self.send_header('Content-Type','audio/wav');self.send_header('Accept-Ranges','bytes');self.send_header('Content-Length',str(len(audio)-offset))
   if offset:self.send_header('Content-Range',f'bytes {offset}-{len(audio)-1}/{len(audio)}')
   self.end_headers()
   try:self.wfile.write(audio[offset:])
   except (BrokenPipeError,ConnectionResetError):pass
  else:self.send_error(404)
server=http.server.ThreadingHTTPServer(('127.0.0.1',0),Fixture);threading.Thread(target=server.serve_forever,daemon=True).start()
out=pathlib.Path(a.output);out.parent.mkdir(parents=True,exist_ok=True)
try:
 subprocess.run([a.java,'-Xmx1024m','-cp',a.classpath,'EchoProbe',a.extension,a.converted,a.entry,f'http://127.0.0.1:{server.server_port}',str(out)],check=True,timeout=60)
 subprocess.run([a.dotnet,str(pathlib.Path(a.project)/'bin/Release/net10.0/BingeX.EchoProbe.dll'),str(out)],check=True,timeout=35)
 assert counts['search']>0 and counts['stream']>0,counts
 print('PASS: authenticated fixture search and original-format stream requests; no real account or public stream used.')
finally:
 print('Fixture request counts:',counts,flush=True);server.shutdown();server.server_close();out.unlink(missing_ok=True)
