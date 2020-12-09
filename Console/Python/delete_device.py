import sys
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
        url=loginHost, headers={'Content-Type': 'application/json'}, data=json.dumps(loginData))
    if not response.ok:
        print('Request failed. status code: {}'.format(response.status_code))
        return None
    jsonObj = response.json()
    return jsonObj['accessToken']


def DeleteDevice(deviceSerialNumber: str, deviceType: str, config: object):
    deviceName = '_'.join([deviceSerialNumber, deviceSerialNumber])
    accessToken = SendRequestLogin(config)
    if not accessToken:
        print('Error: Login failed.')
        return
    print('Login success.')

    host = '/'.join([config['API_DELETE_DEVICE'], 'types',
                     deviceType, 'serialNumbers', deviceSerialNumber])
    print('host: {}'.format(host))
    response = requests.delete(url=host, headers={'Content-Type': 'application/json', 'Authorization': 'Bearer {}'.format(
        accessToken)})
    if response.ok:
        print('Device device {} was deleted successfully.'.format(deviceName))
    else:
        print('Error: cannot delete device {}. status code: {}. {}'.format(
            deviceName, response.status_code, response.text))


if __name__ == "__main__":
    import os
    curr_dir = os.getcwd()
    activate_file = os.path.join(
        curr_dir, 'env', 'Scripts', 'activate_this.py')
    exec(open(activate_file).read(), {'__file__': activate_file})

    import sys
    import requests
    from utils import *

    configFile = sys.argv[1]
    deviceSerialNumber = sys.argv[2]
    deviceType = sys.argv[3]

    config = LoadConfigText(configFile)

    DeleteDevice(deviceSerialNumber, deviceType, config)
