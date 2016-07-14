from flask import Flask, render_template, request, make_response, g
from redis import Redis
from random import choice
from datetime import datetime
import os
import socket
import random
import json

options = []
hostname = socket.gethostname()

app = Flask(__name__)

def get_redis():
    if not hasattr(g, 'redis'):
        g.redis = Redis(host="redis", db=0, socket_timeout=5)
    return g.redis

def get_options():
    global options
    options = []
    redis = get_redis()
    length = redis.llen('options')
    data = redis.lrange('options',0,length)
    for option in data:
        options.append(json.loads(option))

    return options

def get_voted_option(vote):
    options_map = {}
    for opt in options:
        options_map[opt['id']] = opt['name']

    return options_map[vote]

@app.route("/", methods=['POST','GET'])
def hello():
    voter_id = request.cookies.get('voter_id')
    if not voter_id:
        voter_id = hex(random.getrandbits(64))[2:-1]

    vote = None
    voted_option = None

    if request.method == 'POST':
        redis = get_redis()
        vote = request.form['vote']
        voted_option = get_voted_option(vote) 
        data = json.dumps({'voter_id': voter_id, 'vote': vote, 'vote_date': datetime.today().strftime("%d/%m/%Y")})
        redis.rpush('votes', data)

    resp = make_response(render_template(
        'index.html',
        options=get_options(),
        hostname=hostname,
        vote=vote,
        voted_option=voted_option,
    ))
    resp.set_cookie('voter_id', voter_id)
    return resp

if __name__ == "__main__":
    app.run(host='0.0.0.0', port=80, debug=True, threaded=True)
