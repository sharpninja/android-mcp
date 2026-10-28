using System.Text.Json;
using System.Text.Json.Nodes;

namespace AndroidMcp.BuildTool.Documentation.Browser;

// This is browser-only DOM code. It is evaluated by the owned Chromium target,
// never by Node or an external script runner.
internal static class BrowserDom
{
    internal static string Expression(string action,JsonObject? values=null)
    {var input=values is null?new JsonObject():(JsonObject)values.DeepClone();input["action"]=action;return "("+Script+")("+JsonSerializer.Serialize(input)+")";}
    private const string Script="""
    async (arg) => {
      const sections = () => [...document.querySelectorAll('main>section')];
      const active = () => sections().findIndex(s => s.classList.contains('active'));
      const settle = async () => {
        if (document.fonts) await document.fonts.ready;
        await new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)));
      };
      if (arg.action === 'ready') {
        await settle();
        const images = [...document.querySelectorAll('img[alt="Screen"]')];
        const loaded = await Promise.all(images.map(async image => {
          try { await image.decode(); return {valid:image.naturalWidth>0,width:image.naturalWidth,height:image.naturalHeight}; }
          catch { return {valid:false,width:image.naturalWidth,height:image.naturalHeight}; }
        }));
        return {url:location.href,sections:sections().length,font_status:document.fonts ? document.fonts.status : 'unavailable',sources:images.map(i=>i.src),loaded};
      }
      if (arg.action === 'active') { await settle(); return {active:active()}; }
      if (arg.action === 'select') {
        sections().forEach((s,i)=>{s.classList.toggle('active',i===arg.index);s.setAttribute('aria-hidden',String(i!==arg.index));});
        await settle();return {active:active()};
      }
      if (arg.action === 'overflow') {
        await settle();const section=sections()[active()];if(!section)throw Error('Active native slide absent');
        const box=section.getBoundingClientRect(),bad=[];
        const walker=document.createTreeWalker(section,NodeFilter.SHOW_TEXT);
        while(walker.nextNode()) {
          const node=walker.currentNode;if(!node.textContent.trim()||node.parentElement.closest('style,script'))continue;
          const range=document.createRange();range.selectNodeContents(node);
          for(const r of range.getClientRects())if(r.width&&r.height&&(r.left<box.left-1||r.top<box.top-1||r.right>box.right+1||r.bottom>box.bottom+1||r.left< -1||r.top< -1||r.right>innerWidth+1||r.bottom>innerHeight+1))
            bad.push({text:node.textContent,bounds:[r.left,r.top,r.right,r.bottom],section:[box.left,box.top,box.right,box.bottom]});
        }
        return {overflow:bad};
      }
      if (arg.action === 'svg') {
        await settle();const root=document.querySelector('svg');if(!root)throw Error('SVG root absent');
        const view=root.viewBox.baseVal,inverse=root.getScreenCTM().inverse(),outside=[];let checked=0;
        for(const element of root.querySelectorAll('path,text,rect,polygon,polyline,line,circle,ellipse')) {
          if(element.hasAttribute('data-canvas')||element.closest('defs'))continue;
          const b=element.getBBox(),transform=inverse.multiply(element.getScreenCTM());
          const points=[[b.x,b.y],[b.x+b.width,b.y],[b.x,b.y+b.height],[b.x+b.width,b.y+b.height]].map(([x,y])=>new DOMPoint(x,y).matrixTransform(transform));
          const xs=points.map(p=>p.x),ys=points.map(p=>p.y),bounds=[Math.min(...xs),Math.min(...ys),Math.max(...xs),Math.max(...ys)];checked++;
          if(bounds[0]<view.x-1||bounds[1]<view.y-1||bounds[2]>view.x+view.width+1||bounds[3]>view.y+view.height+1)
            outside.push({tag:element.tagName,id:element.id,text:element.tagName==='text'?element.textContent:null,bounds});
        }
        return {viewBox:[view.x,view.y,view.width,view.height],checked,outside};
      }
      if (arg.action === 'contact') {
        const canvas=document.createElementNS('http://www.w3.org/1999/xhtml','canvas');canvas.width=1440;canvas.height=720;
        const context=canvas.getContext('2d');context.fillStyle='white';context.fillRect(0,0,1440,720);
        for(let i=0;i<arg.images.length;i++) {
          const image=new Image();image.src='data:image/png;base64,'+arg.images[i].png;await image.decode();
          const x=(i%4)*360,y=Math.floor(i/4)*240;context.drawImage(image,x,y,360,240);
          context.fillStyle='white';context.fillRect(x,y+220,360,20);context.fillStyle='black';context.font='14px sans-serif';context.fillText('Slide '+arg.images[i].slide,x+8,y+235);
        }
        return {png:canvas.toDataURL('image/png').split(',')[1]};
      }
      throw Error('Unknown browser inspection action '+arg.action);
    }
    """;
}
