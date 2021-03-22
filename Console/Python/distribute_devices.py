

def DivideDevicesAmongAgents(deviceRecords, agentRecords):
    """
    Divide devices list among the connected agents in Round-Robin manner
    """
    for agentRec in agentRecords:
        agentRec['Devices'] = []

    
    agentIndex = 0
    
    for deviceRec in deviceRecords:
        agentRecords[agentIndex]['Devices'].append(deviceRec)
        agentIndex = (agentIndex + 1) % len(agentRecords)

def SendDevice(agentRec):
    print(agentRec['Devices'])
    r = requests.post('http://{}/sendDevices'.format(
            agentRec['URL']), json=agentRec['Devices'])
            
    print('Send json file of devices to agent in url {}'.format(agentRec['URL']))
    print('Request url: {}'.format(agentRec['URL']))
    print('Send devices: {}'.format(agentRec['Devices']))

    if r.status_code == 200:
        print(
            'Agent {} received device list'.format(agentRec['URL']))
    else:
        print('Error occured: {}'.format(r))    

def DistributeDevices(agentsJsonFile, deviceRecords, agentRecords):
    """
    Send to every connected agent a list of devices in json format
    """
    from joblib import Parallel, delayed

    print('Divide devices among agents.')
    DivideDevicesAmongAgents(deviceRecords, agentRecords)
    
    agentDevices = json.dumps(agentRecords)
    WriteContentToFile(agentsJsonFile, agentDevices)

    res = Parallel(n_jobs=len(agentRecords))(delayed(SendDevice)(agentRec) for agentRec in agentRecords)

    #for i, agentRec in enumerate(agentRecords):
    #    print(
    #        'Send json file of devices to agent in url {}'.format(agentRec['URL']))
    #    print('Request url: {}:{}'.format(
    #        agentRec['AgentIP'], agentRec['AgentPort']))
    #    print('Send devices: {}'.format(agentRec['devicesList']))
    #
    #    if res[i].status_code == 200:
    #        print(
    #            'Agent {} received device list'.format(agentRec['URL']))
    #    else:
    #        print('Error occured: {}'.format(r))
        


if __name__ == "__main__":
    from activate_env import *
    ActivateEnv()

    import sys
    import requests
    import traceback
    from utils import *

    print('-----distribute_devices-----')
    print('Arguments: {}'.format(sys.argv))

    try:
        configFile = sys.argv[1]

        if not os.path.exists(configFile):
            print('Error: the config file {} does not exist'.format(configFile))
            exit(2)

        config = LoadConfigText(configFile)

        devicesCsvFile = config['DEVICES_PATH']
        agentsJsonFile = config['AGENTS_PATH']

        print('Read devices file')
        deviceRecords = ReadRecordsFromCsvFile(devicesCsvFile)
        print('Read agents file')
        agentRecords = ReadRecordsFromJsonFile(agentsJsonFile)

        print('Distribute devices to agents')
        DistributeDevices(agentsJsonFile, deviceRecords, agentRecords)

        print('-----success-----')

        #return agentRecords

    except Exception as ex:
        print('Error: {}'.format(ex))
        traceback.print_exc()
        print('-----fail-----')
        exit(1)
