using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ArchiveNull.UI
{
    public sealed partial class ModernOfficeDesktop
    {
        private string settingsTab="General";
        private GameInputAction? pendingBinding;
        private TMP_Text bindingPrompt;
        private int bindingFrame;
        public bool IsSettingsOpen => window!=null && title!=null && title.text.StartsWith("Configuración");
        public bool IsRebinding => pendingBinding.HasValue;
        public void OpenSettings() => ShowSettings(settingsTab);
        private void ShowSettings(string tab)
        {
            pendingBinding=null; settingsTab=tab;
            Window("Configuración  /  "+tab);
            string[] tabs={"General","Sonido","Video","Idioma","Controles"};
            for(int i=0;i<tabs.Length;i++) { string next=tabs[i]; Button(content,next,new Vector2(-450+i*225,265),new Vector2(210,48),()=>ShowSettings(next)); }
            if(tab=="General")
            {
                SettingSlider("Sensibilidad del mouse",150,.25f,8,PlayerPrefs.GetFloat("global.pause.look.sensitivity",2),v=>SetFloat("global.pause.look.sensitivity",v));
                SettingSlider("Movimiento corporal",55,0,1,PlayerPrefs.GetFloat("archive.headbob.intensity",.65f),v=>SetFloat("archive.headbob.intensity",v));
                SettingButton("Ayudas contextuales",PlayerAssistanceSettings.HelpEnabled ? "Activadas" : "Desactivadas",-45,()=>{PlayerAssistanceSettings.HelpEnabled=!PlayerAssistanceSettings.HelpEnabled; ShowSettings(tab);});
                SettingButton("Avisos de acciones",PlayerAssistanceSettings.ActionFeedbackEnabled ? "Activados" : "Desactivados",-135,()=>{PlayerAssistanceSettings.ActionFeedbackEnabled=!PlayerAssistanceSettings.ActionFeedbackEnabled; ShowSettings(tab);});
                Button(content,"Reiniciar ayudas",new Vector2(0,-230),new Vector2(620,48),()=>{PlayerAssistanceSettings.ResetHelpProgress();PlayerAssistanceSettings.HelpEnabled=true;ShowSettings(tab);});
            }
            else if(tab=="Sonido")
            {
                SettingSlider("Volumen maestro",150,0,1,PlayerPrefs.GetFloat("global.pause.master.volume",1),v=>{SetFloat("global.pause.master.volume",v);AudioListener.volume=v;});
                SettingSlider("Efectos y ordenador",45,0,1,GlobalPauseMenu.EffectsVolume,v=>SetFloat("global.pause.effects.volume",v));
                SettingSlider("Voces",-60,0,1,GlobalPauseMenu.VoiceVolume,v=>SetFloat("global.pause.voice.volume",v));
            }
            else if(tab=="Video")
            {
                VideoOption("Calidad",GraphicsSettingsManager.PresetLabel(true),0,()=>GraphicsSettingsManager.CyclePreset());
                VideoOption("Sombras",GraphicsSettingsManager.ShadowLabel(true),1,GraphicsSettingsManager.CycleShadows);
                VideoOption("Texturas",GraphicsSettingsManager.TextureLabel(true),2,GraphicsSettingsManager.CycleTextures);
                VideoOption("Antialiasing",GraphicsSettingsManager.AntiAliasingLabel(),3,GraphicsSettingsManager.CycleAntiAliasing);
                VideoOption("Distancia de sombras",GraphicsSettingsManager.ShadowDistanceLabel(),4,GraphicsSettingsManager.CycleShadowDistance);
                VideoOption("Escala de render",GraphicsSettingsManager.RenderScaleLabel(),5,GraphicsSettingsManager.CycleRenderScale);
                VideoOption("VSync",GraphicsSettingsManager.VSyncLabel(),6,GraphicsSettingsManager.ToggleVSync);
                VideoOption("Límite de FPS",GraphicsSettingsManager.FpsLabel(),7,GraphicsSettingsManager.CycleFps);
            }
            else if(tab=="Idioma")
                SettingButton("Idioma",PlayerPrefs.GetInt("crt.menu.language.index",0)==0 ? "Español" : "English",120,()=>{PlayerPrefs.SetInt("crt.menu.language.index",1-PlayerPrefs.GetInt("crt.menu.language.index",0));PlayerPrefs.Save();ShowSettings(tab);});
            else
            {
                var actions=(GameInputAction[])System.Enum.GetValues(typeof(GameInputAction));
                for(int i=0;i<actions.Length;i++)
                {
                    var action=actions[i];
                    Button(content,GlobalInputBindings.GetLabel(action,true)+"   ·   "+GlobalInputBindings.GetDisplayName(action),new Vector2(i%2==0 ? -285 : 285,170-i/2*57),new Vector2(545,48),()=>{pendingBinding=action;bindingFrame=Time.frameCount;bindingPrompt.text="Pulsá la nueva tecla. Esc cancela.";});
                }
                bindingPrompt=Text(content,"Seleccioná un control para cambiar su tecla.",18,new Vector2(0,-250),new Vector2(1100,40),muted);
            }
        }
        private static void SetFloat(string key,float value) { PlayerPrefs.SetFloat(key,value);PlayerPrefs.Save(); }
        private void SettingSlider(string label,float y,float min,float max,float value,UnityEngine.Events.UnityAction<float> action)
        {
            Text(content,label,23,new Vector2(-285,y),new Vector2(530,45),ink).alignment=TextAlignmentOptions.Left;
            var rect=Panel(content,label+" slider",new Color(.12f,.18f,.25f),new Vector2(240,y),new Vector2(500,24));
            var fill=Panel(rect,"Fill",blue,Vector2.zero,Vector2.zero); fill.anchorMin=Vector2.zero;fill.anchorMax=Vector2.one;fill.offsetMin=fill.offsetMax=Vector2.zero;
            var handle=Panel(rect,"Handle",ink,Vector2.zero,new Vector2(16,38));
            var slider=rect.gameObject.AddComponent<Slider>();slider.minValue=min;slider.maxValue=max;slider.fillRect=fill;slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<Image>();slider.SetValueWithoutNotify(value);
            var number=Text(content,value.ToString("0.00"),18,new Vector2(535,y),new Vector2(100,40),muted);
            slider.onValueChanged.AddListener(v=>{number.text=v.ToString("0.00");action(v);});
        }
        private void SettingButton(string label,string value,float y,UnityEngine.Events.UnityAction action) => Button(content,label+"   /   "+value,new Vector2(0,y),new Vector2(1100,65),action);
        private void VideoOption(string label,string value,int index,UnityEngine.Events.UnityAction action) => Button(content,label+"   /   "+value,new Vector2(index%2==0 ? -285 : 285,155-index/2*100),new Vector2(550,76),()=>{action();ShowSettings("Video");});
        private void UpdateSettingsInput()
        {
            if(!IsOpen || !IsWindowVisible || !IsSettingsOpen || !pendingBinding.HasValue || Time.frameCount==bindingFrame || Keyboard.current==null) return;
            foreach(var key in Keyboard.current.allKeys)
                if(key.wasPressedThisFrame)
                {
                    if(key.keyCode!=Key.Escape) GlobalInputBindings.SetKey(pendingBinding.Value,key.keyCode);
                    ShowSettings("Controles"); break;
                }
        }
    }
}
