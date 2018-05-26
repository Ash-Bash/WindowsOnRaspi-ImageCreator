using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WORPI.ImageCreator
{
    public class DiskItemObject
    {
        //variables
        public int diskNumber { get; set; }
        public string deviceName { get; set; }
        public string mediaType { get; set; }
        public string diskSize { get; set; }

        public DiskItemObject() {

        }

        public DiskItemObject(int diskNumber, string deviceName, string mediaType, string diskSize) {
            this.diskNumber = diskNumber;
            this.deviceName = deviceName;
            this.mediaType = mediaType;
            this.diskSize = diskSize;
        }
    }
}
