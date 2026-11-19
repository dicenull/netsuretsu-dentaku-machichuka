using UnityEngine;

namespace SpinningFeast
{
    // Each voice owns its source, so later sounds never change the pitch of an earlier cue.
    public sealed class FeastSound : MonoBehaviour
    {
        const int VoiceCount=12;
        // Keep the previous BGM preference so players who had opted for silence stay muted.
        const string AudioPreference="feast.music.enabled";
        public static readonly string[] CueNames={"success_small","success_medium","success_large","missing","submit","serve","start","finish","typing","chain","discovery","tick","tick_final","page","select"};
        public static readonly string[] MusicNames={"music_play","music_rush","music_menu"};
        readonly AudioSource[] voices=new AudioSource[VoiceCount];
        readonly float[] gains=new float[VoiceCount];
        readonly double[] started=new double[VoiceCount];
        readonly float[] lastFailure={-10,-10};
        readonly float[] lastSubmit={-10,-10};
        AudioClip small,medium,large,missing,submit,serve,start,finish,typing,chain,discovery,tick,tickFinal,page,select;
        readonly float[] lastType={-10,-10};
        readonly int[] typeNotes={0,0};
        AudioSource musicMain,musicLayer;
        AudioClip musicPlay,musicRush,musicMenu;
        MatchPhase musicPhase=MatchPhase.Title;
        float urgency,endedAt=-100,musicDuck=1;
        bool resultBed;
        public bool AudioEnabled {get;private set;}=true;
        public bool MusicEnabled => AudioEnabled;
        public int CountdownTicks {get;private set;}
        public bool MusicPlaying => musicMain!=null && musicMain.isPlaying;
        public float MusicUrgency => urgency;
        public void Initialize()
        {
            small=Load("success_small");medium=Load("success_medium");large=Load("success_large");
            missing=Load("missing");submit=Load("submit");serve=Load("serve");start=Load("start");finish=Load("finish");
            typing=Load("typing");chain=Load("chain");discovery=Load("discovery");tick=Load("tick");tickFinal=Load("tick_final");page=Load("page");select=Load("select");
            for(int i=0;i<VoiceCount;i++)
            {
                voices[i]=gameObject.AddComponent<AudioSource>();voices[i].playOnAwake=false;
                voices[i].spatialBlend=0;voices[i].dopplerLevel=0;voices[i].loop=false;
            }
            AudioEnabled=PlayerPrefs.GetInt(AudioPreference,1)!=0;
            musicPlay=Load("music_play");musicRush=Load("music_rush");musicMenu=Load("music_menu");
            musicMain=gameObject.AddComponent<AudioSource>();musicLayer=gameObject.AddComponent<AudioSource>();
            foreach(var track in new[]{musicMain,musicLayer}){track.playOnAwake=false;track.loop=true;track.spatialBlend=0;track.dopplerLevel=0;}
            Menu();
        }
        static AudioClip Load(string name)
        {
            var clip=Resources.Load<AudioClip>("Audio/"+name);
            if(!clip)Debug.LogError("Missing sound cue: "+name);
            return clip;
        }
        void Play(AudioClip clip,int player,float gain=1,float pitch=1)
        {
            if(!AudioEnabled || !clip)return;
            int slot=-1;double oldest=double.MaxValue;
            for(int i=0;i<VoiceCount;i++)
            {
                if(!voices[i].isPlaying){slot=i;break;}
                if(started[i]<oldest){oldest=started[i];slot=i;}
            }
            var source=voices[slot];source.Stop();source.clip=clip;
            source.pitch=pitch;source.panStereo=player<0?0:player==0?-.22f:.22f;
            gains[slot]=gain;started[slot]=AudioSettings.dspTime;
            source.volume=.45f*gain/Mathf.Sqrt(VoiceCount);source.Play();
            Balance();
        }
        void Balance()
        {
            int active=0;float summedGain=0;
            for(int i=0;i<voices.Length;i++)if(voices[i] && voices[i].isPlaying){active++;summedGain+=gains[i];}
            // Keep headroom even if many identical cues start together in one simulation tick.
            float master=Mathf.Min(.45f/Mathf.Sqrt(Mathf.Max(1,active)),.8f/Mathf.Max(1,summedGain));
            musicDuck=Mathf.Lerp(1,.65f,Mathf.Clamp01(active/3f));
            for(int i=0;i<VoiceCount;i++)if(voices[i])voices[i].volume=master*gains[i];
        }
        void Update()
        {
            if(AudioEnabled)Balance();
            if(musicPhase==MatchPhase.Result && !resultBed && Time.unscaledTime-endedAt>1.8f)
            {
                resultBed=true;musicMain.Stop();musicMain.clip=musicMenu;musicMain.volume=0;musicMain.Play();
            }
            float baseGain=(musicPhase==MatchPhase.Playing?.14f:musicPhase==MatchPhase.Title?.12f:resultBed?.09f:0)*musicDuck*(AudioEnabled?1:0);
            musicMain.volume=Mathf.MoveTowards(musicMain.volume,baseGain,Time.unscaledDeltaTime*.12f);
            float layerGain=musicPhase==MatchPhase.Playing && AudioEnabled?urgency*.14f*musicDuck:0;
            musicLayer.volume=Mathf.MoveTowards(musicLayer.volume,layerGain,Time.unscaledDeltaTime*.06f);
        }
        public void ToggleMusic()=>ToggleAudio();
        public void ToggleAudio(bool persist=true)
        {
            AudioEnabled=!AudioEnabled;
            if(!AudioEnabled)
            {
                // Stop current one-shots and avoid queueing new ones until sound comes back.
                foreach(var voice in voices)
                {
                    voice.volume=0;voice.Stop();voice.clip=null;
                }
                musicDuck=1;
                // Leave the two loops synchronized and advancing silently across phase changes.
                musicMain.volume=musicLayer.volume=0;
            }
            if(persist)
            {
                PlayerPrefs.SetInt(AudioPreference,AudioEnabled?1:0);
                PlayerPrefs.Save();
            }
        }
        public void SetRemaining(float seconds)
        {urgency=Mathf.Clamp01((30-seconds)/20);}
        public void Menu()
        {
            musicPhase=MatchPhase.Title;urgency=0;resultBed=false;
            musicMain.Stop();musicLayer.Stop();musicMain.clip=musicMenu;musicMain.volume=0;musicMain.Play();
        }
        void PlayMusic()
        {
            musicPhase=MatchPhase.Playing;urgency=0;resultBed=false;CountdownTicks=0;
            musicMain.Stop();musicLayer.Stop();musicMain.clip=musicPlay;musicLayer.clip=musicRush;
            musicMain.volume=musicLayer.volume=0;
            // Both loops share the same frame length and DSP start, so the final layer stays in time.
            double when=AudioSettings.dspTime+.08;musicMain.PlayScheduled(when);musicLayer.PlayScheduled(when);
        }
        public void Cook(int player,int recipe,int count)
        {
            int grade=FeastEffects.GradeForPortions(count);
            Play(grade>=2?large:grade==1?medium:small,player,1,grade==3?1.08f:1);
            if(grade==3)Play(discovery,player,.55f,1.35f);
        }
        public void Missing(int player)
        {if(Time.unscaledTime-lastFailure[player]<.18f)return;lastFailure[player]=Time.unscaledTime;Play(missing,player,.65f);}
        public void Submit(int player)
        {if(Time.unscaledTime-lastSubmit[player]<.06f)return;lastSubmit[player]=Time.unscaledTime;Play(submit,player,.4f);}
        public void Serve(int player,int recipe,int count){Play(serve,player,count>=120?1.1f:count>=40?.85f:.65f,1+(recipe%3)*.03f+(count>=120?.12f:0));}
        public void Eating(int player,int count)
        {
            Play(serve,player,.9f,1.25f);
            Play(count>=10?large:medium,player,.7f,1.12f);
        }
        public void Type(int player)
        {
            if(Time.unscaledTime-lastType[player]<.055f)return;lastType[player]=Time.unscaledTime;
            int n=typeNotes[player]++%4;float pitch=n==0?1:n==1?1.12246f:n==2?1.25992f:1.4983f;
            Play(typing,player,.18f,pitch);
        }
        public void Chain(int player){Play(chain,player,.7f);}
        public void Discover(int player){Play(discovery,player,.75f);}
        public void Tick(int seconds){CountdownTicks++;Play(seconds<=3?tickFinal:tick,-1,seconds<=3?.42f:.28f);}
        public void Lead(int player){Play(select,player,.45f);}
        public void Page(){Play(page,-1,.45f);}
        public void Select(){Play(select,-1,.5f);}
        public void Silence(){foreach(var source in voices)source.Stop();musicMain.Stop();musicLayer.Stop();}
        public void Begin()
        {
            foreach(var source in voices)source.Stop();
            lastFailure[0]=lastFailure[1]=lastSubmit[0]=lastSubmit[1]=lastType[0]=lastType[1]=-10;typeNotes[0]=typeNotes[1]=0;
            Play(start,-1,.8f);PlayMusic();
        }
        public void End(){musicPhase=MatchPhase.Result;endedAt=Time.unscaledTime;resultBed=false;Play(finish,-1,.9f);}
    }
}
