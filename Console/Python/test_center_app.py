import requests
from flask import Flask, jsonify, request, Response, abort
from wsgiref.simple_server import make_server
from test_center import *
from utils import *
import threading
import sys

"""
Run the flask application:
set FLASK_APP=test_center_app.py
flask run --host 0.0.0.0
"""


app = Flask(__name__)

testCenter = None


@app.route('/')
def Index():
    return jsonify({'About': 'LumenisX Test Center'})


@app.route('/connect', methods=["GET"])
def Connect():
    """
    Connect to test center - registers agent to its pool if not exist
    """
    # Get agent request containing IP and port
    agentIP = request.remote_addr
    agentPort = request.args.get('agent_port')
    if not agentPort:
        abort(400, {'Bad request': 'Request does not have agentPort argument'})
    testCenter.RegisterAgent(agentIP, agentPort)
    testCenter.WriteLog('Registered agents:', 'info')
    for agentObj in testCenter.agents:
        testCenter.WriteLog(agentObj, 'info')

    return jsonify({'Ack': 'Agent-{} was registered'.format(agentPort)}, 200)


@ app.route('/disconnect', methods=["GET"])
def Disconnect():
    """
    Disconnects from test center - unregister agent from pool
    """
    agentIP = request.remote_addr
    agentPort = request.args.get('agent_port')
    if not agentPort:
        abort(400, {'Bad request': 'Request does not have agentPort argument'})
    if not testCenter.AgentExists(agentIP, agentPort):
        abort(400, {'Error': 'Agent-{} not exist'.format(agentPort)})
    testCenter.RemoveAgent(agentIP, agentPort)
    return jsonify({'Agent-{} was disconnected'.format(agentPort)}, 200)


@ app.route('/agentReady', methods=["GET"])
def AgentReady():
    """
    Get from agent that it is ready to get automation script - send automation script
    """
    pass
    # agentIP = request.remote_addr
    # agentPort = request.args.get('agent_port')
    # if not agentPort:
    #     abort(400, {'Bad request': 'Request does not have agentPort argument'})
    # if not testCenter.AgentExists(agentIP, agentPort):
    #     abort(400, {'Error': 'Agent-{} not exist'.format(agentPort)})
    # agentObj = testCenter.GetAgent(agentIP, agentPort)
    # testCenter.SendAutomationScript(agentObj)


@ app.route('/agentsNumber')
def GetAgentsNumber():
    return jsonify({'AgentsNum': len(testCenter.agents)})


@ app.route('/reset', methods=["GET"])
def Reset():
    testCenter.agents.clear()
    return jsonify({'state': 'reset'})


if __name__ == '__main__':
    testCenter = TestCenter('config.ini')

    testCenter.WriteLog(
        'Test center {} was created.'.format(testCenter), 'info')
    testCenter.WriteLog('Max agents: {}'.format(testCenter.maxAgents), 'info')

    # Collect devices list from portal
    testCenter.WriteLog('Collect devices list from portal', 'info')
    testCenter.devicesList = testCenter.CollectDevicesFromPortal()
    testCenter.WriteLog('Devices from portal: {}'.format(
        len(testCenter.devicesList)), 'info')

    if len(testCenter.devicesList) == 0:
        testCenter.WriteLog('No devices.', 'error')
        exit(1)

    # Write devices list to csv file
    csvFilePath = testCenter.GetConfigParam('csv', 'devicesPath')
    testCenter.WriteLog(
        'Write devices list to csv file {}'.format(csvFilePath), 'info')
    WriteToCsvFile(csvFilePath, testCenter.devicesList)

    # Collect AWS services and instances from env (defined in config file)
    testCenter.WriteLog('Collect AWS service and instances', 'info')

    # Start thread waiting for all agents to be connected before distriburting the devices to them
    distributeThread = threading.Thread(
        target=testCenter.DistributeDevicesToAgents)
    distributeThread.start()

    # Run rest-api on port 5000
    testCenter.WriteLog('Run rest-api on port 5000', 'info')
    app.run(debug=True, host='0.0.0.0')
