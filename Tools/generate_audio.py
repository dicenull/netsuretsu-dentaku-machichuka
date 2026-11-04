"""Generate original, deterministic, stereo arcade cooking cues. Python standard library only."""
from pathlib import Path
import math, random, wave, array, json
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/SpinningFeast/Resources/Audio'
OUT.mkdir(parents=True,exist_ok=True)
SR=48000
rng=random.Random(20261104)

def cue(seconds):return [[0.0]*int(seconds*SR),[0.0]*int(seconds*SR)]
def mix(buf,start,duration,freq,amp=.3,pan=0,kind='mallet'):
    offset=int(start*SR);phase=0;lp=0
    for i in range(int(duration*SR)):
        j=offset+i
        if j>=len(buf[0]):break
        t=i/SR;u=t/duration
        attack=min(1,t/.004);tail=min(1,(duration-t)/.025)
        f=freq
        if kind=='bubble': f=freq*(.65+.7*math.exp(-t*20)+.28*math.sin(t*14))
        if kind=='kick': f=65+130*math.exp(-t*40)
        phase+=2*math.pi*f/SR
        noise=rng.uniform(-1,1);lp=.8*lp+.2*noise
        if kind=='mallet':
            tone=(math.sin(phase+1.8*math.exp(-t*22)*math.sin(2*phase))+.24*math.sin(phase*2.76)*math.exp(-t*18)+.12*math.sin(phase*4.12)*math.exp(-t*26))
            envelope=math.exp(-t*8)
        elif kind=='bell':
            tone=math.sin(phase)+.33*math.sin(2.76*phase)+.16*math.sin(5.4*phase)
            envelope=math.exp(-t*11)
        elif kind=='bubble':
            tone=math.sin(phase+.8*math.sin(2*phase))* .85+.12*math.sin(3*phase)
            envelope=math.exp(-t*14)
        elif kind=='clap':
            tone=noise-lp
            envelope=math.exp(-t*48)+.6*math.exp(-abs(t-.014)*190)+.35*math.exp(-abs(t-.029)*150)
        elif kind=='shaker':tone=noise-lp;envelope=math.exp(-t*65)
        elif kind=='kick':tone=math.sin(phase);envelope=math.exp(-t*23)
        elif kind=='bass':tone=math.sin(phase)+.20*math.sin(2*phase)+.06*math.sin(3*phase);envelope=math.exp(-t*5)
        else: # Warm plucked chord, with a restrained high-frequency component.
            tone=math.sin(phase)+.24*math.sin(2*phase)+.10*math.sin(3*phase)
            envelope=math.exp(-t*6)
        value=tone*amp*attack*tail*envelope
        l=math.sqrt((1-pan)/2);r=math.sqrt((1+pan)/2)
        buf[0][j]+=value*l;buf[1][j]+=value*r
        # Two quiet stereo reflections give the toy instruments a small, warm room.
        if kind in ('mallet','bell','chord'):
            for delay,gain in ((.073,.13),(.137,.065)):
                k=j+int(delay*SR)
                if k<len(buf[0]):buf[0][k]+=value*r*gain;buf[1][k]+=value*l*gain

def hz(midi):return 440*2**((midi-69)/12)
metrics={}
def save(name,buf,loop=False):
    peak=max(max(abs(x) for x in ch) for ch in buf)
    gain=min(2.5,.72/max(peak,1e-8))
    data=array.array('h');power=0;final_peak=0
    for i in range(len(buf[0])):
        fade=1 if loop else min(1,i/(SR*.003),(len(buf[0])-1-i)/(SR*.018))
        for ch in buf:
            value=ch[i]*gain*max(0,fade);power+=value*value;final_peak=max(final_peak,abs(value));data.append(round(value*32767))
    with wave.open(str(OUT/(name+'.wav')),'wb') as f:f.setnchannels(2);f.setsampwidth(2);f.setframerate(SR);f.writeframes(data.tobytes())
    rms=math.sqrt(power/(len(buf[0])*2))
    assert .02<rms<.35 and final_peak<=.721,(name,rms,final_peak)
    metrics[name]={'seconds':round(len(buf[0])/SR,3),'peak':round(final_peak,4),'rms':round(rms,4),'channels':2,'sample_rate':SR,'loop':loop}
    return buf

for tier,notes in enumerate(([79,84,88],[72,76,79,84],[72,76,79,81,84,88])):
    b=cue(.85+tier*.15)
    mix(b,0,.14,400,.38,kind='bubble');mix(b,0,.16,90,.24,kind='kick')
    for i,n in enumerate(notes):mix(b,.035+i*.095,.42,hz(n),.27,(-.3 if i%2==0 else .3))
    for n in (60,64,67):mix(b,.12,.50,hz(n),.075,kind='chord')
    if tier>=1:
        mix(b,.19,.10,0,.15,kind='clap');mix(b,.38,.10,0,.12,kind='clap')
    if tier==2:
        mix(b,.51,.35,hz(96),.10,.4,'bell');mix(b,.42,.09,0,.09,-.4,'shaker')
    save(('success_small','success_medium','success_large')[tier],b)
b=cue(.53);mix(b,0,.17,420,.27,-.15,'bubble');mix(b,.14,.20,620,.22,.15,'bubble');mix(b,.23,.22,hz(79),.10,0,'mallet');save('missing',b)
b=cue(.18);mix(b,0,.13,750,.16,kind='bubble');save('submit',b)
b=cue(.46);mix(b,0,.24,1550,.19,-.15,'bell');mix(b,.06,.23,2010,.12,.15,'bell');mix(b,0,.15,90,.1,kind='kick');save('serve',b)
b=cue(1.0)
for i,n in enumerate((72,79,84)):mix(b,i*.15,.45,hz(n),.25,(i-1)*.2)
mix(b,.3,.13,0,.14,kind='clap');save('start',b)
b=cue(1.7)
for i,n in enumerate((76,79,84,88)):mix(b,i*.13,.5,hz(n),.24,(i%2-.5)*.4)
for n in (60,64,67,72):mix(b,.53,.85,hz(n),.11,kind='chord')
mix(b,.53,.15,0,.18,kind='clap');mix(b,.65,.6,hz(96),.07,.4,'bell');save('finish',b)
# Short feedback layers for typing, menus, discoveries and the final countdown.
b=cue(.12);mix(b,0,.10,hz(79),.11,kind='bubble');save('typing',b)
b=cue(.64)
for i,n in enumerate((76,79,84,88)):mix(b,i*.075,.32,hz(n),.20,(i%2-.5)*.35)
mix(b,.15,.12,0,.09,kind='clap');save('chain',b)
b=cue(1.1)
for i,n in enumerate((84,88,91,96)):mix(b,i*.12,.47,hz(n),.17,(i%2-.5)*.5,'bell')
for n in (72,76,79):mix(b,.28,.55,hz(n),.07,kind='chord')
save('discovery',b)
b=cue(.20);mix(b,0,.16,hz(72),.22,kind='mallet');save('tick',b)
b=cue(.25);mix(b,0,.19,hz(84),.22,kind='mallet');mix(b,.07,.12,hz(91),.10,kind='bell');save('tick_final',b)
b=cue(.26);mix(b,0,.14,520,.17,-.2,'bubble');mix(b,.055,.15,780,.13,.2,'bubble');save('page',b)
b=cue(.31);mix(b,0,.18,hz(79),.19,kind='mallet');mix(b,.07,.19,hz(84),.17,.2,'mallet');save('select',b)
# Original eight-bar loops. Tail samples wrap to the beginning for a seamless seam.
def finish_loop(name,buf,frames):
    for ch in buf:
        for i in range(frames,len(ch)):ch[i-frames]+=ch[i]
        del ch[frames:]
    result=save(name,buf,loop=True)
    seam=max(abs(ch[0]-ch[-1]) for ch in result)
    assert seam<.025,(name,'loop seam',seam)
    metrics[name]['seam_delta']=round(seam,6)

beat=60/112;frames=round(32*beat*SR)
base=cue(frames/SR+1.2);rush=cue(frames/SR+1.2)
chords=((60,64,67,69),(65,69,72,76),(62,65,69,72),(67,71,74,77),
        (60,64,67,69),(69,72,76,79),(65,69,72,76),(67,71,74,77))
melodies=((76,79,81,79,76,74,72),(77,81,84,81,79,77),(74,77,81,79,77,74),
          (74,79,83,81,79,77,74),(76,79,84,83,81,79),(81,84,88,84,81,79),
          (77,81,84,81,79,77),(79,83,86,83,79,74,72))
for bar,notes in enumerate(chords):
    at=bar*4*beat
    for n in notes:
        for t in (0,1.5,2.75):mix(base,at+t*beat,.55,hz(n),.031,(-.28 if n%2 else .28),'chord')
    root=notes[0]-24
    for t,n in ((0,root),(1.5,root+7),(2.5,root),(3.5,root+12)):
        mix(base,at+t*beat,.32,hz(n),.17,0,'bass')
    for j,n in enumerate(melodies[bar]):
        t=(j*.5+(.07 if j%2 else 0))*beat
        mix(base,at+t,.31,hz(n),.062,(-.2 if j%2==0 else .2),'mallet')
    for j in range(4):
        mix(base,at+j*beat,.13,80,.09,0,'kick')
        if j%2:mix(base,at+j*beat,.08,0,.037,.15,'clap')
        for t in (0,.55):mix(base,at+(j+t)*beat,.055,0,.022,-.2,'shaker')
        mix(rush,at+(j+.5)*beat,.17,hz(notes[j]+24),.055,(j%2-.5)*.5,'bell')
        mix(rush,at+(j+.75)*beat,.055,0,.055,.25,'shaker')
        if j%2:mix(rush,at+j*beat,.09,0,.08,-.1,'clap')
finish_loop('music_play',base,frames);finish_loop('music_rush',rush,frames)
menuBeat=60/88;menuFrames=round(32*menuBeat*SR);menu=cue(menuFrames/SR+1.2)
for bar,notes in enumerate(chords):
    at=bar*4*menuBeat
    for j,n in enumerate(notes):mix(menu,at+j*.12,.75,hz(n),.07,(j-1.5)*.13,'chord')
    for j in range(4):mix(menu,at+(j+.5)*menuBeat,.6,hz(notes[j]+12),.045,(j%2-.5)*.5,'mallet')
    mix(menu,at,.6,hz(notes[0]-12),.12,0,'bass')
finish_loop('music_menu',menu,menuFrames)
(ROOT/'Docs/audio-metrics.json').write_text(json.dumps(metrics,indent=2)+'\n')
# A short, reproducible audition containing success, a large batch, shortage, and serving.
preview=cue(12.0)
for name,at in [('typing',0),('typing',.2),('submit',.4),('success_medium',.6),('success_large',1.8),('missing',3.1),('serve',3.8),('chain',4.4),('discovery',5.3),('page',6.6),('select',7.1),('tick',7.7),('tick_final',8.2),('finish',8.8)]:
    with wave.open(str(OUT/(name+'.wav')),'rb') as f:samples=array.array('h',f.readframes(f.getnframes()))
    for i in range(len(samples)//2):
        j=int(at*SR)+i
        if j<len(preview[0]):
            preview[0][j]+=samples[i*2]/32768;preview[1][j]+=samples[i*2+1]/32768
with wave.open(str(ROOT/'Docs/audio-preview.wav'),'wb') as f:
    f.setnchannels(2);f.setsampwidth(2);f.setframerate(SR)
    pcm=array.array('h',(round(max(-.95,min(.95,preview[c][i]))*32767) for i in range(len(preview[0])) for c in range(2)))
    f.writeframes(pcm.tobytes())
# A mixed audition of the adaptive music and a few cooking cues.
showcase=cue(16)
for name,gain,start in [('music_play',.25,0),('music_rush',.30,0),('success_medium',.45,2.8),('chain',.35,6.5),('discovery',.38,10.2),('finish',.45,13.7)]:
    with wave.open(str(OUT/(name+'.wav')),'rb') as f:samples=array.array('h',f.readframes(f.getnframes()))
    for i in range(len(samples)//2):
        j=int(start*SR)+i
        if j>=len(showcase[0]):break
        layer=max(0,min(1,(j/SR-7)/4)) if name=='music_rush' else 1
        showcase[0][j]+=samples[i*2]/32768*gain*layer
        showcase[1][j]+=samples[i*2+1]/32768*gain*layer
with wave.open(str(ROOT/'Docs/music-preview.wav'),'wb') as f:
    f.setnchannels(2);f.setsampwidth(2);f.setframerate(SR)
    data=array.array('h',(round(max(-.95,min(.95,showcase[c][i]))*32767) for i in range(len(showcase[0])) for c in range(2)))
    f.writeframes(data.tobytes())
print(f'PASS: {sum(not m["loop"] for m in metrics.values())} stereo cues and {sum(m["loop"] for m in metrics.values())} music loops; bounded peaks, non-silent RMS, seamless loops; previews generated.')
