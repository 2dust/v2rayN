using System;
using v2rayN.Mode;

namespace v2rayN.Handler
{
    internal class QuickSelectHandler
    {
        public List<SelectItem> Items { get; }
        public List<SelectItem> validItems { get; set; }
        public QuickSelectHandler()
        {
            Items = new List<SelectItem>();
            validItems = new List<SelectItem>();
        }
        public bool selectContent; 
        
        public void add(string indexid)
        {
            Items.Add(new SelectItem(indexid));
        }
        public void itemAdd(string indexid,string delay)
        {
            var item = Items.Find(it=>it.indexId == indexid);
            if (item == null)
            {
                var additem = new SelectItem(indexid);
                Items.Add(additem);
                additem.push(delay);
            }
            else
            {
                item.push(delay);
            }
        }
        public List<string> select()
        {
            List<string> invaliditems = new List<string>();
            var itv0 = Items.FindAll(it => it.E == -1);
            var itv2or3 = Items.FindAll(it => it.ValidValue == 2 || it.ValidValue == 3);
            var itv1 = Items.FindAll(it => it.ValidValue == 1);
            validItems.AddRange(itv2or3);
            foreach(var i in itv0)
            {
                invaliditems.Add(i.indexId);
            }
            if (itv2or3.Any())
            {
                selectContent = true;
                foreach (var i in itv1)
                {
                    invaliditems.Add(i.indexId);
                }
            }
            else
            {
                selectContent = false;
                validItems.AddRange(itv1.FindAll(it => !it.getDelay(2).Trim().Equals("-1")));
                foreach (var i in itv1.FindAll(it => it.getDelay(2).Trim().Equals("-1")))
                {
                    invaliditems.Add(i.indexId);
                }
            }
            return invaliditems;
        }

        public void clear()
        {
            Items.Clear();
            validItems.Clear();
        }
    }
}
