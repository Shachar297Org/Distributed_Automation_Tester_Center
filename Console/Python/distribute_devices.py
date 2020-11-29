import os
import sys
import requests
from utils import *


def DivideDevicesAmongAgents(deviceRecords, agentRecords):
    """
    Divide devices list among the connected agents in Round-Robin manner
    """
    for agentRec in agentRecords:
        agentRec['devicesList'] = []

    agentIndex = 0
    for deviceRec in deviceRecords:
        agentRecords[agentIndex]['devicesList'].append(deviceRec)
        agentIndex = (agentIndex + 1) % len(agentRecords)


def DistributeDevices(deviceRecords, agentRecords):
    """
    Send to every connected agent a list of devices in json format
    """
    print('Divide devices among agents.')
    DivideDevicesAmongAgents(deviceRecords, agentRecords)
    for agentRec in agentRecords:
        print(
            'Send json file of devices to agent-{}'.format(agentRec['AgentPort']))
        r = requests.post('http://{}:{}/sendDevices'.format(
            agentRec['AgentIP'], agentRec['AgentPort']), json=agentRec['devicesList'])
        if r.status_code == 200:
            print(
                'Agent-{} received device list'.format(agentRec['AgentPort']))


if __name__ == "__main__":
    try:
        devicesFile = sys.argv[1]
        agentsFile = sys.argv[2]

        if not os.path.exists(devicesFile):
            print('Error: the file {} does not exist'.format(devicesFile))
            exit(2)

        if not os.path.exists(agentsFile):
            print('Error: the file {} does not exist'.format(agentsFile))
            exit(2)

        deviceRecords = ReadRecordsFromCsvFile(devicesFile)
        agentRecords = ReadRecordsFromJsonFile(agentsFile)

        DistributeDevices(deviceRecords, agentRecords)

    except Exception as ex:
        print('Error: {}'.format(ex))
        exit(1)
