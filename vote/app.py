from flask import Flask, render_template, request, make_response, g
from redis import Redis
from random import choice
from datetime import datetime
import os
import socket
import random
import json

option_a = os.getenv('OPTION_A', "Cats")
option_b = os.getenv('OPTION_B', "Dogs")
option_c = os.getenv('OPTION_C', "Birds")
hostname = socket.gethostname()

app = Flask(__name__)

def get_options():
    choices = ["Alphaiate", "Shopping - Praca", "Boteco", "Maria Maria", "Pizzaria Atlantico", "Sushi-Tay San", "Sushi - Nirai", "Bar do Lula - Final da Imbiribeira","Michelli","Camarada","Bode", "Parraxaxa", "Dom ferreiro", "Emporio","Carcara","The Fifties","Saturdays", "So caldinho","Cangaco","Restaurante de Allan", "Galletus","Chica Pitanga"]

    if datetime.today().weekday() == 4:
        choices.append("Paranoia")

    return choice(choices)

def get_redis():
    if not hasattr(g, 'redis'):
        g.redis = Redis(host="redis", db=0, socket_timeout=5)
    return g.redis

def get_voted_option(vote):
    return {
        'a': option_a,
        'b': option_b,
        'c': option_c,
    }[vote]

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
        option_a=option_a,
        option_b=option_b,
        option_c=option_c,
        hostname=hostname,
        vote=vote,
        voted_option=voted_option,
    ))
    resp.set_cookie('voter_id', voter_id)
    return resp

if __name__ == "__main__":
    app.run(host='0.0.0.0', port=80, debug=True, threaded=True)
