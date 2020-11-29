import os
from sql import *
from utils import *
import configparser
import requests
import time
import json
from logger import *


class TestCenter:
    def __init__(self, configFile: str):
        self.configFile = configFile
        self.configParser = configparser.ConfigParser()
        self.configParser.read(configFile, encoding='utf-8')
        self.maxAgents = int(self.GetConfigParam('Agents', 'maxAgents'))
        self.devicesList = []
        self.agents = []
        self.connectedAgents = []
        self.instances = []
        logFile = self.GetConfigParam('Logger', 'loggerFile')
        self.logger = Logger('Test-Center', logFile)

    def __str__(self):
        return 'Test Center - port 5000'

    def GetConfigParam(self, section: str, param: str):
        """
        Return value from config file
        """
        return self.configParser.get(section, param)

    def WriteLog(self, msg: str, level: str):
        """
        Write message to log file
        """
        self.logger.WriteLog(msg, level)

    def ConnectRDS(self, host: str, db: str, username: str, password: str):
        """
        Connect to RDS and return connection object
        """
        dbConn = None
        try:
            dbConn = DbConnector(host, db, username, password)
            if dbConn:
                return dbConn
            else:
                return None
        except Exception as ex:
            print(ex)
            return None

    def AgentExists(self, agentIP: str, agentPort: int):
        """
        Check if agent exists in agents list
        """
        for agentObj in self.agents:
            if agentIP == agentObj['ip'] and str(agentPort) == str(agentObj['port']):
                return True
        return False

    def RegisterAgent(self, agentIP: str, agentPort: int):
        """
        Register agent by adding it to the agents list
        """
        for agentObj in self.agents:
            if agentObj['ip'] == agentIP and str(agentObj['port']) == str(agentPort):
                print('Agent is already registered.')
                return
        self.agents.append({'ip': agentIP, 'port': agentPort})

    def RemoveAgent(self, agentIP: str, agentPort: int):
        """
        Unregister agent by removing the agent from the agents list
        """
        self.agents.remove({'ip': agentIP, 'port': agentPort})

    def GetAgent(self, agentIP, agentPort):
        for agentObj in self.agents:
            if agentObj['ip'] == agentIP and agentObj['port'] == agentPort:
                return agentObj
        return None

    def GetAgents(self):
        """
        Return agents list
        """
        return self.agents

    def GetDeviceEntries(self, dbConn: object):
        """
        Return all device entries from database
        """
        tableName = '.'.join(['DeviceService', 'Devices'])
        query = "SELECT * FROM {} as devices left join DeviceService.BaseDevice as baseDevices on devices.manufacturerId=baseDevices.manufacturerId".format(
            tableName)
        resultsSet = dbConn.ExecuteQuery(tableName, query)
        return resultsSet

    def CollectDevicesFromPortal(self):
        """
        Collect devices list from the portal and return list of their records {GA, SN}
        """
        config = ReadConfigFile('config.ini')
        host = config['RDSConnection']['host']
        db = config['RDSConnection']['db']
        username = config['RDSConnection']['username']
        password = config['RDSConnection']['password']

        # Connect to RDS
        simFlag = self.GetConfigParam('Debug', 'simulate')
        if simFlag:
            return [
                {'deviceType': 'GA-0000080CN', 'deviceSerialNumber': 'ELAD-TEST-1'},
                {'deviceType': 'GA-0000080CN', 'deviceSerialNumber': 'ELAD-TEST-2'},
                {'deviceType': 'GA-0000080CN', 'deviceSerialNumber': 'ELAD-TEST-3'},
                {'deviceType': 'GA-0000080CN', 'deviceSerialNumber': 'ELAD-TEST-4'},
                {'deviceType': 'GA-0000080CN', 'deviceSerialNumber': 'ELAD-TEST-5'},
                {'deviceType': 'GA-0000080CN', 'deviceSerialNumber': 'ELAD-TEST-6'},
                {'deviceType': 'GA-0000080CN', 'deviceSerialNumber': 'ELAD-TEST-7'},
                {'deviceType': 'GA-0000080CN', 'deviceSerialNumber': 'ELAD-TEST-8'},
                {'deviceType': 'GA-0000180', 'deviceSerialNumber': 'ELAD-TEST-9'},
                {'deviceType': 'GA-0000180', 'deviceSerialNumber': 'ELAD-TEST-10'},
                {'deviceType': 'GA-0000180', 'deviceSerialNumber': 'ELAD-TEST-11'},
                {'deviceType': 'GA-0000080CN', 'deviceSerialNumber': 'ELAD-TEST-12'},
                {'deviceType': 'GA-0000180',
                    'deviceSerialNumber': 'ELAD-TEST-13'},  # new
                {'deviceType': 'GA-0000180',
                    'deviceSerialNumber': 'ELAD-TEST-14'},  # new
                {'deviceType': 'GA-0000180',
                    'deviceSerialNumber': 'ELAD-TEST-15'},  # new
            ]
        else:
            deviceRecords = []
            dbConn = self.ConnectRDS(host, db, username, password)
            if dbConn:
                self.WriteLog('Connected to RDS.', 'info')
                deviceEntries = self.GetDeviceEntries(dbConn)
                deviceRecords = [ConvertEntryToDeviceRecord(
                    entry) for entry in deviceEntries]
            else:
                self.WriteLog('Cannot connect to RDS.', 'error')

        return deviceRecords

    def DivideDevicesAmongAgents(self):
        """
        Divide devices list among the connected agents in Round-Robin manner
        """
        for agentObj in self.agents:
            agentObj['devicesList'] = []

        agentIndex = 0
        for device in self.devicesList:
            self.agents[agentIndex]['devicesList'].append(device)
            agentIndex = (agentIndex+1) % len(self.connectedAgents)

    def DistributeDevicesToAgents(self):
        """
        Send to every connected agent a list of devices in json format
        """
        self.WriteLog('Wait 30 sec before starting the distibution', 'info')
        waitTime = int(self.GetConfigParam(
            'Delay', 'waitTimeBeforeDistributeInSec'))
        time.sleep(waitTime)

        try:
            # Check what agents are still connected
            self.WriteLog('Check what agents are still connected.', 'info')
            for agentObj in self.agents:
                r = requests.get(
                    'http://{}:{}/'.format(agentObj['ip'], agentObj['port']))
                if r.status_code == 200:
                    self.connectedAgents.append(agentObj)

            self.WriteLog('Divide devices among agents.', 'info')
            self.DivideDevicesAmongAgents()

            for agentObj in self.connectedAgents:
                self.WriteLog(
                    'Send json file of devices to agent-{}'.format(agentObj['port']), 'info')
                r = requests.post(
                    'http://{}:{}/sendDevices'.format(agentObj['ip'], agentObj['port']), json=agentObj['devicesList'])
                if r.status_code == 200 and 'ready' in r.text:
                    self.WriteLog(
                        'Sending automation script to agent-{}'.format(agentObj['port']), 'info')
                    self.SendAutomationScript(agentObj)
        except Exception as ex:
            self.WriteLog('Error: {}'.format(ex), 'error')

    def SendAutomationScript(self, agentObj: object):
        """
        Send post request to ready agent with script txt file
        """
        scriptFilePath = self.configParser.get(
            'Scripts', 'activationScriptFilePath')
        if not os.path.exists(scriptFilePath):
            self.WriteLog('Script file {} not exist'.format(
                scriptFilePath), 'error')
            return
        fileContent = open(scriptFilePath, 'r').read()
        self.WriteLog(
            'Sending activation script to agent-{}'.format(agentObj['port']), 'info')
        r = requests.post(
            url='http://{}:{}/sendScript'.format(agentObj['ip'], agentObj['port']), json={'type': 'txt', 'content': fileContent})
        if r.status_code == 200:
            self.WriteLog('Script was recieved at agent {}:{}'.format(
                agentObj['ip'], agentObj['port']), 'info')

        self.WriteLog('Finish.', 'info')

    def GetReportsFromAgent(self, agent):
        pass

    def SendRequestLogin(self):
        """
        Sends to api host login request and return access token
        """
        loginHost = self.GetConfigParam('api', 'login_host')
        loginData = {
            "email": self.GetConfigParam('api', 'username'),
            "password": self.GetConfigParam('api', 'password')
        }
        response = requests.post(
            url=loginHost, headers={'Content-Type': 'application/json'}, data=json.dumps(loginData))
        if not response.ok:
            self.WriteLog('Request failed. status code: {}'.format(
                response.status_code), 'error')
            return None
        jsonObj = response.json()
        return jsonObj['accessToken']

    def InsertDevice(self, accessToken: str, deviceRecord: dict):
        """
        Insert new device to portal using access token
        """
        host = self.GetConfigParam('api', 'insert_device')
        response = requests.post(url=host, headers={'Content-Type': 'application/json', 'Authorization': 'Bearer {}'.format(
            accessToken)}, data=json.dumps({'createLumDeviceRequest': deviceRecord}))
        if response.ok:
            self.WriteLog('Device device {} was inserted successfully.'.format(
                deviceRecord), 'info')
        else:
            self.WriteLog('Error: cannot insert device {}. status code: {}. {}'.format(
                deviceRecord, response.status_code, response.text), 'error')

    def InsertDevices(self, csvFile: str):
        """
        Insert new devices from csv file to portal
        """
        accessToken = self.SendRequestLogin()
        if not accessToken:
            self.WriteLog('Login failed.', 'error')
            return
        self.WriteLog('Login success.', 'info')
        deviceRecords = ReadRecordsFromCsvFile(csvFile)
        for deviceRecord in deviceRecords:
            self.WriteLog('Inserting device: {}'.format(deviceRecord), 'info')
            self.InsertDevice(accessToken, deviceRecord)

    def GetServices(self, env):
        """
        Return list of AWS services in env
        """
        out = RunCommand(
            'aws ecs list-services --cluster {}-ECS-Cluster'.format(env))
        out = out.replace('\r\n', '').replace(' ', '')
        return json.loads(out)['serviceArns']

    def GetInstances(self):
        """
        Return list of AWS instances
        """
        out = RunCommand('aws ec2 describe-instances')
        out = out.replace('\r\n', '').replace(' ', '')
        instanceObjects = []
        reservations = json.loads(out)['Reservations']
        for reservationObj in reservations:
            instanceObjects.extend(reservationObj['Instances'])
        self.instances = []
        for instanceObj in instanceObjects:
            self.instances.append({'instanceId': instanceObj['InstanceId'], 'dnsName': instanceObj['PublicDnsName'],
                                   'state': instanceObj['State']['Name']})
        return self.instances

    def GetInstanceState(self, instanceId: str):
        """
        arg instanceId - instance id\n
        Return instance state
        """
        self.GetInstances()
        for instance in self.instances:
            if instanceId == instance['instanceId']:
                return instance['state']
        return None

    def StopService(self, env, service):
        if env not in ['dev', 'int', 'staging']:
            raise Exception(
                'Unknown environment {} - must be dev, int or staging'.format(env))
        taskString = RunCommand(
            'aws ecs list-tasks --cluster {}-ECS-Cluster --service-name {}-{}-Service --output text --query taskArns[0]'.format(env, env, service))
        taskId = taskString[taskString.rfind('/') + 1:].strip()
        result = RunCommand(
            'aws ecs stop-task --cluster {}-ECS-Cluster --task {}'.format(env, taskId))
        return result


if __name__ == "__main__":
    testCenter = TestCenter('config.ini')
    # services = testCenter.GetServices()
    # print(services)

    # csvFile = testCenter.GetConfigParam('csv', 'devicesPath')
    # testCenter.InsertDevices(csvFile)

    # status = testCenter.GetInstanceState('i-01be7347226b06ce2')
    # print(status)

    # status = testCenter.GetInstanceState('i-07ece630e506d88ef')
    # print(status)

    # result = testCenter.StopService('int', 'Alarm')
    # print(result)

    # out = RunCommand('aws ec2 describe-instance-status')
    # print(out)

    # instances = testCenter.GetInstances()
    # for instance in instances:
    #     print(instance)
    # print('#instances: {}'.format(len(instances)))
