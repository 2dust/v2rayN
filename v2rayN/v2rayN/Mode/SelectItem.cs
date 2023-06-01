using System;

namespace v2rayN.Mode
{
    internal class SelectItem
    {
        public string indexId { get; }
        private string[] delay =  new string[3];
        private int idx = -1;
        private int validvalue  = 0;
        public int ValidValue
        {
            get { return validvalue; }
        }
        public SelectItem(string id) {
            indexId = id;
        }
        private int e = 0;
        public int E
        {
            get { return e; }
        }

        public string getDelay(int i)
        {   if (i >= 0 && i <= 2)
            {
                return delay[i];
            }
            else { return ""; }
        }

        public void push(string dl)
        {   
            if(idx<2)
            {
                delay[++idx] = dl;
                if (!dl.Trim().Equals("-1"))
                {
                    validvalue++;
                }
                if(idx == 2)
                {
                    e = 0;
                    foreach(var v in delay)
                    {
                        if(v != null && !v.Trim().Equals("-1"))
                        {
                            e += int.Parse(v);
                        }                       
                    }
                    if (validvalue == 0)
                    {
                        e = -1;
                    }
                    else
                    {
                        e /= validvalue;
                    }
                }
            }
        }

        public string pop()
        {
            if (idx >= 0)
            {
                if (!delay[idx].Trim().Equals("-1"))
                {
                    validvalue--;
                }
                return delay[idx--];
            }
            return "";
        }



        

    }
}
