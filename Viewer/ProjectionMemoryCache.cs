using System;
using System.Collections.Generic;

namespace QuickLook.DicomRT
{
    // Caller holds the projection-cache lock. A scan of unused views must not
    // evict playback frames; both queues still share one hard memory limit.
    internal sealed class ProjectionMemoryCache<T> where T:class
    {
        sealed class Entry { public T Value; public int Bytes; public bool Active; public LinkedListNode<string> Node; }
        readonly int budget;
        readonly Func<T,int> size;
        int bytes;
        readonly Dictionary<string,Entry> items=new Dictionary<string,Entry>();
        readonly LinkedList<string> cold=new LinkedList<string>(),active=new LinkedList<string>();
        public ProjectionMemoryCache(int megabytes,Func<T,int> size){budget=megabytes*1024*1024;this.size=size;}
        public T Get(string key,bool touch)
        {
            Entry entry;if(!items.TryGetValue(key,out entry))return null;
            if(touch){(entry.Active?active:cold).Remove(entry.Node);entry.Active=true;entry.Node=active.AddLast(key);}
            return entry.Value;
        }
        public void Put(string key,T value){Store(key,value,false);}
        public void PutActive(string key,T value){Store(key,value,true);}
        void Store(string key,T value,bool requested)
        {
            int count=size(value);if(count>budget)return;
            Entry previous;if(items.TryGetValue(key,out previous)){requested|=previous.Active;Remove(key);}
            while(bytes+count>budget||items.Count>=24000){
                if(cold.Count>0)Remove(cold.First.Value);
                else if(requested&&active.Count>0)Remove(active.First.Value);
                else return;
            }
            items[key]=new Entry{Value=value,Bytes=count,Active=requested,Node=(requested?active:cold).AddLast(key)};bytes+=count;
        }
        void Remove(string key){var entry=items[key];(entry.Active?active:cold).Remove(entry.Node);bytes-=entry.Bytes;items.Remove(key);}
        public void Clear(){items.Clear();cold.Clear();active.Clear();bytes=0;}
    }
}
