import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
plt.figure()
plt.bar(['A', 'B', 'C'], [3, 7, 5])
plt.title('Inline categories')
plt.savefig(r"{OUTPUT_PNG}", dpi=150, bbox_inches='tight')
