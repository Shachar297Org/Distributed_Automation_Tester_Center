import hashlib
from utils import *


"""
This class describes a record of an event message
The record contains:
- Device type
- Device serial number
- Entry key - event type
- Entry value - event data
- Entry timestamp time of the event
"""


class Record:
    def __init__(self, deviceType: str, serialNum: str, entryKey: str, entryValue: str, entryTimeStamp: str):
        """
        Create new Record object
        Arguments: device type, serial number, entry key, entry value and creation time
        """
        self.deviceType = deviceType
        self.serialNum = serialNum
        self.entryKey = entryKey
        self.entryValue = entryValue
        self.entryTimeStamp = entryTimeStamp

    def __str__(self):
        """
        Return toString value of the record
        """
        return ','.join([self.deviceType, str(self.serialNum), self.entryKey, self.entryValue, self.entryTimeStamp])

    def Hash(self):
        """
        Return hash value of the record
        """
        word = str.encode('_'.join([self.deviceType, str(self.serialNum),
                                    self.entryKey, self.entryValue, self.entryTimeStamp]))
        hashObj = hashlib.sha256(word)
        return hashObj.hexdigest()

    def GetDeviceType(self):
        return self.deviceType

    def GetSerialNum(self):
        return self.serialNum

    def GetEntryKey(self):
        return self.entryKey

    def GetEntryValue(self):
        return self.entryValue

    def GetEntryTimeStamp(self):
        return self.entryTimeStamp

    def ToDict(self):
        return {'deviceType': self.deviceType, 'deviceSerialNumber': self.serialNum, 'entryKey': self.entryKey, 'entryValue': self.entryValue, 'entryTimeStamp': self.entryTimeStamp}


if __name__ == "__main__":
    t = GenerateNowTime()
    record1 = Record('GA-001', 224, 'Power', 'Shutdown', t)
    record2 = Record('GA-001', 224, 'Power', 'Shutdown', t)

    print(record1)
    print(record2.Hash())

    print(record2)
    print(record2.Hash())
