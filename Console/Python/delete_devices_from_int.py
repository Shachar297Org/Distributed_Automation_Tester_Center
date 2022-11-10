import configparser
import json
import traceback

import requests

from utils import *


def SendRequestLogin(config: object):
    """
    Sends to api host login request and return access token
    """
    loginHost = config['API_LOGIN_HOST']
    loginData = {
        "email": config['API_USER'],
        "password": config['API_PASS']
    }
    response = requests.post(
        url=loginHost, headers={'Content-Type': 'application/json'}, data=json.dumps(loginData), timeout=100)
    if not response.ok:
        print('Request failed. status code: {}'.format(response.status_code))
        return None
    jsonObj = response.json()
    return jsonObj['accessToken']

def CollectDevicesFromPortalByAPI(config: object, limit=0, page=0, searchQuery='AUTOTESTS-'):
    """
    Collect devices list from the portal by API and return list of their records {GA, SN}
    """
    accessToken = SendRequestLogin(config)

    if not accessToken:
        print('Error: Login failed.')
        return
    print('Login success. ')

    response = None
    devices = []

    if limit == 0 and page == 0:
        host = config['API_GET_DEVICES_URL'] + \
            '?limit=1&page=0&search={}'.format(searchQuery)

        response = requests.get(url=host, headers={'Content-Type': 'application/json', 'Authorization': 'Bearer {}'.format(
            accessToken)}, timeout=100)

        totalResults = None

        if response.ok:
            totalResults = response.json()['metadata']['page']['totalResults']
            print("total: {}".format(totalResults))

            if totalResults < 500:
                host = config['API_GET_DEVICES_URL'] + \
                    '?limit={}&page=0&search={}'.format(totalResults, searchQuery)

                print('host: {}'.format(host))
                response = requests.get(url=host, headers={'Content-Type': 'application/json', 'Authorization': 'Bearer {}'.format(
                    accessToken)}, timeout=100)

                for device_json in response.json()['data']:
                    device = device_json['deviceInfo']
                    #print("la")
                    if re.search("(-\d+)\Z", device['deviceSerialNumber']):
                        print("Adding device {0} {1}".format(device['deviceSerialNumber'], device['deviceType']))
                        devices.append(
                            {'deviceType': device['deviceType'], 'deviceSerialNumber': device['deviceSerialNumber']})
            else:
                index = 0
                while totalResults > 0:
                    host = config['API_GET_DEVICES_URL'] + \
                        '?limit={}&page={}&search={}'.format(500, index, searchQuery)

                    print('host: {}'.format(host))
                    response = requests.get(url=host, headers={'Content-Type': 'application/json', 'Authorization': 'Bearer {}'.format(
                        accessToken)}, timeout=100)

                    for device_json in response.json()['data']:
                        device = device_json['deviceInfo']
                        #print(re.match("[a-zA-Z]+-[0-9]+", device['deviceSerialNumber']))
                        if re.search("(-\d+)\Z", device['deviceSerialNumber']):
                            print("Adding device {0} {1}".format(device['deviceSerialNumber'], device['deviceType']))
                            devices.append(
                                {'deviceType': device['deviceType'], 'deviceSerialNumber': device['deviceSerialNumber']})

                    index = index + 1
                    totalResults = totalResults - 500
    else:
        host = config['API_GET_DEVICES_URL'] + \
            '?limit={0}&page={1}&search={2}'.format(limit, page, searchQuery)
        print('host: {}'.format(host))

        response = requests.get(url=host, headers={'Content-Type': 'application/json', 'Authorization': 'Bearer {}'.format(
            accessToken)}, timeout=100)

        for device_json in response.json()['data']:
            device = device_json['deviceInfo']
            devices.append(
                {'deviceType': device['deviceType'], 'deviceSerialNumber': device['deviceSerialNumber']})

    if response and response.ok:
        print('Devices were retrieved successfully.')
    else:
        print('Error: cannot retrieve the devices. status code: {}. {}'.format(
            response.status_code, response.text))
    

    print('Number of devices collected: {}'.format(len(devices)))

    return devices, accessToken

import re

from joblib import Parallel, delayed

from activate_env import *
from delete_device import DeleteDevice, DeleteDevices

ActivateEnv()
#print(re.search("(-\d+)\Z", "AUTOTESTS-08550995-w"))

config = LoadConfigText("D:\\Config\\test_center_config.txt")

portalDeviceRecords, accessToken = CollectDevicesFromPortalByAPI(
            config)

Parallel(n_jobs=8)(delayed(DeleteDevice)(accessToken, device['deviceSerialNumber'], device['deviceType'], config) for device in portalDeviceRecords)
#for device in portalDeviceRecords:
#    DeleteDevices(accessToken, portalDeviceRecords, config)