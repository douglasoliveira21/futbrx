import unittest,importlib.util,pathlib,copy
p=pathlib.Path(__file__).with_name('build-reviewed-catalog.py');spec=importlib.util.spec_from_file_location('builder',p);module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
class ReviewedCatalogTests(unittest.TestCase):
 def setUp(self):
  self.clubs=[dict(id='club'+str(i),name='Test Club '+str(i),short='T',color='#123456',strength=70) for i in range(20)]
  self.rows=[dict(entity='https://www.wikidata.org/entity/Q'+str(1000+i*18+j),clubId=c['id'],name='Test player '+str(j),position='GOL' if j<2 else 'MEI',birthDate='2000-12-01',membershipVerified=True,verifiedOn='2026-11-21',membershipSource='https://example.test/squad') for i,c in enumerate(self.clubs) for j in range(18)]
 def test_complete(self):
  catalog=module.build(self.clubs,self.rows,'2026-11-21');self.assertEqual(len(catalog['players']),360);self.assertEqual(catalog['players'][0]['player']['age'],25)
 def test_incomplete(self):
  with self.assertRaises(ValueError):module.build(self.clubs,self.rows[:-1],'2026-11-21')
 def test_unverified(self):
  self.rows[0]['membershipVerified']=False
  with self.assertRaises(ValueError):module.build(self.clubs,self.rows,'2026-11-21')
 def test_duplicate(self):
  self.rows[-1]['entity']=self.rows[0]['entity']
  with self.assertRaises(ValueError):module.build(self.clubs,self.rows,'2026-11-21')
 def test_wrong_date(self):
  self.rows[0]['verifiedOn']='2025-11-21'
  with self.assertRaises(ValueError):module.build(self.clubs,self.rows,'2026-11-21')
if __name__=='__main__':unittest.main()
