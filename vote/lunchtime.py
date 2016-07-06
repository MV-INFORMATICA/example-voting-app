from random import choice
from datetime import datetime
choices = ["Alphaiate", "Shopping - Praca", "Boteco", "Maria Maria", "Pizzaria Atlantico", "Sushi-Tay San", "Sushi - Nirai", "Bar do Lula - Final da Imbiribeira","Michelli","Camarada","Bode", "Parraxaxa", "Dom ferreiro", "Emporio","Carcara","The Fifties","Saturdays", "So caldinho","Cangaco","Restaurante de Allan", "Galletus","Chica Pitanga"]

if datetime.today().weekday() == 4:
	choices.append("Paranoia")

print("today we will eat at", choice(choices))