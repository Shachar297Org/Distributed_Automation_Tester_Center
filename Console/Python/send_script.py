import os
import sys
import requests
from utils import *


def SendScriptToAgent(agentRec, scriptContent, configParser, logger):
    logger.WriteLog(
        'Sending activation script to agent-{}'.format(agentRec['AgentPort']), 'info')
    r = requests.post(
        url='http://{}:{}/sendScript'.format(agentRec['AgentIP'], agentRec['AgentPort']), json={'Type': 'txt', 'Content': scriptContent})
    if r.status_code == 200:
        logger.WriteLog('Script was recieved at agent {}:{}'.format(
            agentRec['AgentIP'], agentRec['AgentPort']), 'info')


def SendScriptToAgents(agentRecords, scriptContent, configParser, logger):
    for agentRec in agentRecords:
        if agentRec['IsReady']:
            SendScriptToAgent(agentRec, scriptContent, configParser, logger)


if __name__ == "__main__":
    try:
        agentsFile = sys.argv[1]
        scriptFile = sys.argv[2]

        if not os.path.exists(agentsFile):
            print('Error: the file {} does not exist'.format(agentsFile))
            exit(2)

        if not os.path.exists(scriptFile):
            print('Error: the file {} does not exist'.format(scriptFile))
            exit(2)

        agentRecords = ReadRecordsFromJsonFile(agentsFile)
        scriptContent = ReadContentFromFile(scriptFile)

        configParser = LoadConfig()
        logger = InitLogger(configParser)

        SendScriptToAgents(agentRecords, scriptContent, configParser, logger)

    except Exception as ex:
        print('Error: {}'.format(ex))
        exit(1)
