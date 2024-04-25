import json


all_cards = json.load(open('all_cards.json'))

rarities = []

for card in all_cards['data']:
    if 'card_sets' in card:
        for set in card['card_sets']:
            str = set['set_rarity'] + ' ' + set['set_rarity_code']
            if str not in rarities:
                rarities.append(str)

[print(x) for x in rarities]