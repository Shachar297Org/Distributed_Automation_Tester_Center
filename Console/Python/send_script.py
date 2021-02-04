

def SendScriptToAgent(agentRec: dict, scriptContent: str, delayBeforeStoppingServer: int):
    print(
        'Sending activation script to agent {}'.format(agentRec['URL']), 'info')
    r = requests.post(
        url='http://{}:{}/sendScript'.format(agentRec['AgentIP'], agentRec['AgentPort']), json={'Type': 'txt', 'Content': scriptContent, 'StoppingDelay' : delayBeforeStoppingServer})
    if r.status_code == 200:
        print('Script was recieved at agent {}'.format(
            agentRec['URL']), 'info')
    else:
        print('Error: {}: {}'.format(r.status_code, r.reason))


def SendScriptToAgents(agentRecords: list, scriptContent: str, delayBeforeStoppingServer: int):
    from joblib import Parallel, delayed

    res = Parallel(n_jobs=len(agentRecords))(delayed(SendScriptToAgent)(agentRec, scriptContent, delayBeforeStoppingServer) for agentRec in agentRecords)

    #for agentRec in agentRecords:
    #    if agentRec['IsReady']:
    #        SendScriptToAgent(agentRec, scriptContent, delayBeforeStoppingServer)


if __name__ == "__main__":
    from activate_env import *
    ActivateEnv()

    import sys
    import requests
    from utils import *

    print('-----send_script-----')
    print('Arguments: {}'.format(sys.argv))

    try:
        configFile = sys.argv[1]

        if not os.path.exists(configFile):
            print('Error: the config file {} does not exist'.format(configFile))
            exit(2)

        config = LoadConfigText(configFile)

        agentsFile = config['AGENTS_PATH']
        scriptFile = config['ACTIVATION_SCRIPT_PATH']
        delayBeforeStoppingServer = 0

        if int(config['SCENARIO']) == 2:
            delayBeforeStoppingServer = 2*int(config['MINUTES_TO_KEEP_STOPPED'])

        agentRecords = ReadRecordsFromJsonFile(agentsFile)
        scriptContent = ReadContentFromFile(scriptFile)

        SendScriptToAgents(agentRecords, scriptContent, delayBeforeStoppingServer)
        print('-----success-----')

    except Exception as ex:
        print('Error: {}'.format(ex))
        print('-----fail-----')
        exit(1)
