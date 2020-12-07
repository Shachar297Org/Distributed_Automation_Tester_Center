

def SendScriptToAgent(agentRec: dict, scriptContent: str):
    print(
        'Sending activation script to agent {}'.format(agentRec['URL']), 'info')
    r = requests.post(
        url='http://{}:{}/sendScript'.format(agentRec['AgentIP'], agentRec['AgentPort']), json={'Type': 'txt', 'Content': scriptContent})
    if r.status_code == 200:
        print('Script was recieved at agent {}'.format(
            agentRec['URL']), 'info')
    else:
        print('Error: {}: {}'.format(r.status_code, r.reason))


def SendScriptToAgents(agentRecords: list, scriptContent: str):
    for agentRec in agentRecords:
        if agentRec['IsReady']:
            SendScriptToAgent(agentRec, scriptContent)


if __name__ == "__main__":
    import os
    curr_dir = os.getcwd()
    activate_file = os.path.join(
        curr_dir, 'env', 'Scripts', 'activate_this.py')
    exec(open(activate_file).read(), {'__file__': activate_file})

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

        agentRecords = ReadRecordsFromJsonFile(agentsFile)
        scriptContent = ReadContentFromFile(scriptFile)

        SendScriptToAgents(agentRecords, scriptContent)
        print('-----success-----')

    except Exception as ex:
        print('Error: {}'.format(ex))
        print('-----fail-----')
        exit(1)
