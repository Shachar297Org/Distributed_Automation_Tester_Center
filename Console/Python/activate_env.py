import os


def ActivateEnv():
    curr_dir = os.getcwd()
    activate_file = os.path.join(
        curr_dir, 'env', 'Scripts', 'activate_this.py')
    exec(open(activate_file).read(), {'__file__': activate_file})
